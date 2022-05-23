﻿using SCFirstOrderLogic.Inference.Resolution.Utility;
using SCFirstOrderLogic.SentenceFormatting;
using SCFirstOrderLogic.SentenceManipulation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Resolution
{
    /// <summary>
    /// Implementation of <see cref="IQuery"/> used by <see cref="SimpleResolutionKnowledgeBase"/>.
    /// Encapsulates a query, allowing for step-by-step execution, as well as examination of those steps, as or after they are carried out.
    /// Notes:
    /// <list type="bullet">
    /// <item/>Not thread-safe (i.e. not re-entrant) - despite the fact that resolution is ripe for parallelisation.
    /// </list>  
    /// </summary>
    public class SimpleResolutionQuery : IQuery
    {
        private readonly IQueryClauseStore clauseStore;
        private readonly Func<(CNFClause, CNFClause), bool> clausePairFilter;
        private readonly MaxPriorityQueue<(CNFClause, CNFClause, VariableSubstitution, CNFClause)> queue;
        private readonly Dictionary<CNFClause, (CNFClause, CNFClause, VariableSubstitution)> steps;

        private bool result;

        private SimpleResolutionQuery(
            IKnowledgeBaseClauseStore clauseStore,
            Func<(CNFClause, CNFClause), bool> clausePairFilter,
            Comparison<(CNFClause, CNFClause)> clausePairPriorityComparison,
            Sentence query)
        {
            this.clauseStore = clauseStore.CreateQueryClauseStore();
            this.clausePairFilter = clausePairFilter;
            queue = new MaxPriorityQueue<(CNFClause, CNFClause, VariableSubstitution, CNFClause)>((x, y) =>
            {
                return clausePairPriorityComparison((x.Item1, x.Item2), (y.Item1, y.Item2));  // TODO: ugh, hideous (and perhaps slow - test me)
            });
            steps = new Dictionary<CNFClause, (CNFClause, CNFClause, VariableSubstitution)>();

            NegatedQuery = new CNFSentence(new Negation(query));
        }

        /// <summary>
        /// Gets the (CNF representation of) the negation of the query.
        /// </summary>
        public CNFSentence NegatedQuery { get; }

        /// <inheritdoc/>
        public bool IsComplete { get; private set; } = false;

        /// <inheritdoc/>
        public bool Result
        {
            get
            {
                if (!IsComplete)
                {
                    throw new InvalidOperationException("Query is not yet complete");
                }

                return result;
            }
        }

        /// <summary>
        /// Gets a mapping from a clause to the two clauses from which it was inferred. NB: we use binary resolution,
        /// hence two input clauses.
        /// </summary>
        public IReadOnlyDictionary<CNFClause, (CNFClause clause1, CNFClause clause2, VariableSubstitution unifier)> Steps => steps;

        /// <summary>
        /// Creates and initialises a new instance of the <see cref="Query"/> class.
        /// Initialisation can potentially be a long-running operation (and long-running constructors are a bad idea) -
        /// hence this method exists and the constructor is private.
        /// </summary>
        /// <param name="clauseStore">A knowledge base clause store to use to create the clause store for this query.</param>
        /// <param name="clausePairFilter">A filter to apply to clause pairings during this query.</param>
        /// <param name="clausePairPriorityComparison">A comparison delegate to use to prioritise clause pairings during this query.</param>
        /// <param name="querySentence">The query itself.</param>
        /// <param name="querySentence">The cancellation token for this operation.</param>
        /// <returns>A new query instance.</returns>
        internal static async Task<SimpleResolutionQuery> CreateAsync(
            IKnowledgeBaseClauseStore clauseStore,
            Func<(CNFClause, CNFClause), bool> clausePairFilter,
            Comparison<(CNFClause, CNFClause)> clausePairPriorityComparison,
            Sentence querySentence,
            CancellationToken cancellationToken = default)
        {
            var query = new SimpleResolutionQuery(clauseStore, clausePairFilter, clausePairPriorityComparison, querySentence);

            // Initialise the clause store with the clauses from the negation of the query:
            foreach (var clause in query.NegatedQuery.Clauses)
            {
                await clauseStore.AddAsync(clause, cancellationToken);
            }

            // Queue up all initial clause pairings - adhering to our clause pair filter and priority comparer.
            await foreach (var clause in clauseStore)
            {
                await query.EnqueueUnfilteredResolventsAsync(clause, cancellationToken);
            }

            return query;
        }

        /// <inheritdoc/>
        public async Task NextStepAsync(CancellationToken cancellationToken = default)
        {
            if (IsComplete)
            {
                throw new InvalidOperationException("Query is complete");
            }

            // Grab the next resolvent (in addition to the two clauses it originates from and the variable substitution) from the queue..
            var (ci, cj, unifier, resolvent) = queue.Dequeue();
            steps[resolvent] = (ci, cj, unifier);

            // If the resolvent is an empty clause, we've found a contradiction and can thus return a positive result:
            if (resolvent.Equals(CNFClause.Empty))
            {
                steps[CNFClause.Empty] = (ci, cj, unifier);
                result = true;
                IsComplete = true;
                return;
            }

            // Otherwise, check if we've found a new clause (i.e. something that we didn't know already)..
            // Upside of using Add to implicitly check for existence: it means we don't need a separate "Contains" method
            // on the store (which would raise potential misunderstandings about what the store means by "contains" - c.f. subsumption..)
            // Downside of using Add: clause store will encounter itself when looking for unifiers - not a big deal,
            // but a performance/maintainability tradeoff nonetheless
            if (clauseStore.AddAsync(resolvent, cancellationToken).Result)
            {
                // This is a new clause, so we queue up some more clause pairings -
                // (combinations of the resolvent and existing known clauses)
                // adhering to any filtering and ordering we have in place.

                // ..and iterate through its resolvents (if any) - also make a note of the unifier so that we can include it in the record of steps that we maintain:
                await EnqueueUnfilteredResolventsAsync(resolvent, cancellationToken);
            }

            // If we've run out of clauses to smash together, return a negative result.
            if (queue.Count == 0)
            {
                result = false;
                IsComplete = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            clauseStore.Dispose();
        }

        /// <summary>
        /// Retrieves a list of the (useful) clauses discovered by a query that has returned a positive result.
        /// Starts with clauses that were discovered using only clauses from the knowledge base or negated
        /// query, and ends with the empty clause.
        /// </summary>
        /// <returns>A list of the discovered clauses of the given query.</returns>
        /// <exception cref="InvalidOperationException">If the query is not complete, or returned a negative result.</exception>
        public ReadOnlyCollection<CNFClause> GetDiscoveredClauses()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Query is not yet complete");
            }
            else if (!Result)
            {
                throw new InvalidOperationException("Explanation of a negative result (which could be massive) is not supported");
            }

            // Walk back through the DAG of clauses, starting from CNFClause.Empty, breadth-first:
            var orderedSteps = new List<CNFClause>();
            var queue = new Queue<CNFClause>(new[] { CNFClause.Empty });
            while (queue.Count > 0)
            {
                var clause = queue.Dequeue();
                if (orderedSteps.Contains(clause))
                {
                    // We have found the same clause "earlier" than another encounter of it.
                    // Remove the "later" one so that once we reverse the list, there are no
                    // references to clauses we've not seen yet.
                    orderedSteps.Remove(clause);
                }

                // If the clause is an intermediate one from the query (as opposed to one found
                // in the knowledge base or negated query)..
                if (Steps.TryGetValue(clause, out var input))
                {
                    // ..queue up the two clauses that resolved to give us this one..
                    queue.Enqueue(input.clause1);
                    queue.Enqueue(input.clause2);

                    // ..and record it in the steps list.
                    orderedSteps.Add(clause);
                }
            }

            // Reverse the steps encountered in the backward pass, to obtain a list of steps
            // contributing to the result, in (roughly..) the order in which query found them.
            orderedSteps.Reverse();

            return orderedSteps.AsReadOnly();
        }

        /// <summary>
        /// Produces a (very raw) explanation of the steps that led to the result of the query.
        /// </summary>
        /// <returns>A (very raw) explanation of the steps that led to the result of the query.</returns>
        public string Explain()
        {
            var formatter = new SentenceFormatter();

            if (!IsComplete)
            {
                throw new InvalidOperationException("Query is not yet complete");
            }
            else if (!Result)
            {
                throw new InvalidOperationException("Explanation of a negative result (which could be massive) is not supported");
            }

            var discoveredClauses = GetDiscoveredClauses();

            // Now build the explanation string.
            var explanation = new StringBuilder();
            for (var i = 0; i < discoveredClauses.Count; i++)
            {
                var (clause1, clause2, unifier) = Steps[discoveredClauses[i]];

                string GetSource(CNFClause clause)
                {
                    if (discoveredClauses.Contains(clause))
                    {
                        return $"#{discoveredClauses.IndexOf(clause):D2}";
                    }
                    else if (NegatedQuery.Clauses.Contains(clause))
                    {
                        return " ¬Q";
                    }
                    else
                    {
                        return " KB";
                    }
                }

                string ExplainNormalisationTerm(Term term)
                {
                    if (term is Function function && function.Symbol is SkolemFunctionSymbol skolemFunctionSymbol)
                    {
                        return $"some {formatter.Print(skolemFunctionSymbol.StandardisedVariableSymbol)} from {formatter.Print(skolemFunctionSymbol.OriginalSentence)}";
                    }
                    else if (term is VariableReference variable && variable.Symbol is StandardisedVariableSymbol standardisedVariableSymbol)
                    {
                        return $"a standardisation of {standardisedVariableSymbol.OriginalSymbol} from {formatter.Print(standardisedVariableSymbol.OriginalSentence)}";
                    }
                    else
                    {
                        return $"something unrecognised. Something has gone wrong..";
                    }
                }

                explanation.AppendLine($"#{i:D2}: {formatter.Print(discoveredClauses[i])}");
                explanation.AppendLine($"     From {GetSource(clause1)}: {formatter.Print(clause1)}");
                explanation.AppendLine($"     And  {GetSource(clause2)}: {formatter.Print(clause2)} ");
                explanation.Append("     Using   : {");
                explanation.Append(string.Join(", ", unifier.Bindings.Select(s => $"{formatter.Print(s.Key)}/{formatter.Print(s.Value)}")));
                explanation.AppendLine("}");
                foreach (var term in FindNormalisationTerms(discoveredClauses[i], clause1, clause2))
                {
                    explanation.AppendLine($"     ..where {formatter.Print(term)} is {ExplainNormalisationTerm(term)}");
                }

                explanation.AppendLine();
            }

            return explanation.ToString();
        }

        /// <summary>
        /// Returns an enumeration of all of the Terms created by the normalisation process (as opposed to featuring in the original sentences).
        /// That is, standardised variables and Skolem functions. Intended to be useful in creating a "legend" of such terms.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<Term> FindNormalisationTerms(params CNFClause[] clauses)
        {
            var returnedAlready = new List<Term>();

            foreach (var literal in clauses.SelectMany(c => c.Literals))
            {
                foreach (var topLevelTerm in literal.Predicate.Arguments)
                {
                    var stack = new Stack<Term>();
                    stack.Push(topLevelTerm);
                    while (stack.Count > 0)
                    {
                        var term = stack.Pop();
                        switch (term)
                        {
                            case Function function:
                                if (function.Symbol is SkolemFunctionSymbol && !returnedAlready.Contains(function))
                                {
                                    returnedAlready.Add(function);
                                    yield return function;
                                }

                                foreach (var argument in function.Arguments)
                                {
                                    stack.Push(argument);
                                }

                                break;
                            case VariableReference variable:
                                if (variable.Symbol is StandardisedVariableSymbol && !returnedAlready.Contains(variable))
                                {
                                    returnedAlready.Add(variable);
                                    yield return variable;
                                }

                                break;
                        }
                    }
                }
            }
        }

        private async Task EnqueueUnfilteredResolventsAsync(CNFClause clause, CancellationToken cancellationToken = default)
        {
            await foreach (var (otherClause, unifier, unified) in clauseStore.FindResolutions(clause, cancellationToken))
            {
                // NB: Throwing away clauses returned by the unifier store has performance impact.
                // Could instead/also use a store that knows to not look for certain clause pairings in the first place..
                // However, REQUIRING the store to do this felt a little ugly from a code perspective, since the store is
                // then a mix of implementation (how unifiers are stored/indexed) and strategy, plus there's a bit more
                // strategy in the form of the priority comparer. This feels a good compromise - there are of course
                // alternatives (e.g. some kind of strategy object that encapsulates both) - but they felt like overkill
                // for this simple implementation.
                if (clausePairFilter((clause, otherClause)))
                {
                    queue.Enqueue((clause, otherClause, unifier, unified));
                }
            }
        }
    }
}