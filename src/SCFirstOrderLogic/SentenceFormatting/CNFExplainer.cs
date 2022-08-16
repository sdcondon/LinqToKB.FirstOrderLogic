﻿using SCFirstOrderLogic.SentenceManipulation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SCFirstOrderLogic.SentenceFormatting
{
    /// <summary>
    /// Utility logic for human-readable explanations of CNF clauses - notably, the CNF terms (standardised variables and Skolem functions) within them.
    /// </summary>
    public class CNFExplainer
    {
        private readonly SentenceFormatter sentenceFormatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="CNFExplainer"/> class.
        /// </summary>
        /// <param name="sentenceFormatter"></param>
        public CNFExplainer(SentenceFormatter sentenceFormatter)
        {
            this.sentenceFormatter = sentenceFormatter;
        }

        /// <summary>
        /// Returns an enumeration of all of the Terms created by the normalisation process (as opposed to featuring in the original sentences).
        /// That is, standardised variables and Skolem functions. Intended to be useful in creating a "legend" of such terms.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Term> FindNormalisationTerms(params CNFClause[] clauses)
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

        /// <summary>
        /// Outputs a human-readble string for a given normalisaion term (standardised variable or Skolem function). Intended for use with <see cref="FindNormalisationTerms(CNFClause[])"/>.
        /// <para/>
        /// TODO-LOCALISATION: (if I get bored or this ever takes off for whtaever reason): Allow for localisation and allow specification of culture in ctor (optional - default to current culture).
        /// </summary>
        /// <param name="term">The term to examine.</param>
        /// <returns>A human readable string that completes the sentence "{term} is .."</returns>
        public string ExplainNormalisationTerm(Term term)
        {
            if (term is Function function && function.Symbol is SkolemFunctionSymbol skolemFunctionSymbol)
            {
                return $"some {sentenceFormatter.Format(skolemFunctionSymbol.StandardisedVariableSymbol)} from {sentenceFormatter.Format(skolemFunctionSymbol.OriginalSentence)}";
            }
            else if (term is VariableReference variable && variable.Symbol is StandardisedVariableSymbol standardisedVariableSymbol)
            {
                return $"a standardisation of {standardisedVariableSymbol.OriginalSymbol} from {sentenceFormatter.Format(standardisedVariableSymbol.OriginalSentence)}";
            }
            else
            {
                throw new ArgumentException("The provided term is not a standardised variable or Skolem function", nameof(term));
            }
        }
    }
}