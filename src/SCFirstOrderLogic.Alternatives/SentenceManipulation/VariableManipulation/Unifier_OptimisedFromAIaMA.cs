﻿// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System.Diagnostics.CodeAnalysis;

namespace SCFirstOrderLogic.SentenceManipulation.VariableManipulation;

/// <summary>
/// Most general unifier logic - optimised from the version presented in the source material,
/// but operating on entire sentences, as opposed to literals. More powerful, but slower.
/// This class is intended as a more realistic baseline than <see cref="Unifier_FromAIaMA"/>.
/// </summary>
public static class Unifier_OptimisedFromAIaMA
{
    public static bool TryUnify(Sentence x, Sentence y, [NotNullWhen(returnValue: true)] out IDictionary<VariableReference, Term>? unifier)
    {
        unifier = new Dictionary<VariableReference, Term>();

        if (!TryUnify(x, y, unifier))
        {
            unifier = null;
            return false;
        }

        return true;
    }

    private static bool TryUnify(Sentence x, Sentence y, IDictionary<VariableReference, Term> unifier)
    {
        // NB: type switch likely to be slower than using visitor pattern
        return (x, y) switch
        {
            (Conjunction conjunctionX, Conjunction conjunctionY) => TryUnify(conjunctionX, conjunctionY, unifier),
            (Disjunction disjunctionX, Disjunction disjunctionY) => TryUnify(disjunctionX, disjunctionY, unifier),
            (Equivalence equivalenceX, Equivalence equivalenceY) => TryUnify(equivalenceX, equivalenceY, unifier),
            //(ExistentialQuantification existentialQuantificationX, ExistentialQuantification existentialQuantificationY) => TryUnify(existentialQuantificationX, existentialQuantificationY, unifier),
            (Implication implicationX, Implication implicationY) => TryUnify(implicationX, implicationY, unifier),
            (Negation negationX, Negation negationY) => TryUnify(negationX, negationY, unifier),
            (Predicate predicateX, Predicate predicateY) => TryUnify(predicateX, predicateY, unifier),
            //(UniversalQuantification universalQuantificationX, UniversalQuantification universalQuantificationY) => TryUnify(universalQuantificationX, universalQuantificationY, unifier),
            _ => false
        };
    }

    private static bool TryUnify(Conjunction x, Conjunction y, IDictionary<VariableReference, Term> unifier)
    {
        // WOULD-BE-A-BUG-IF-THIS-WERE-PROD-CODE: Order shouldn't matter (but need to be careful about partially updating unifier)
        // perhaps Low and High (internal) props in conjunction?
        return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
    }

    private static bool TryUnify(Disjunction x, Disjunction y, IDictionary<VariableReference, Term> unifier)
    {
        // WOULD-BE-A-BUG-IF-THIS-WERE-PROD-CODE: Order shouldn't matter (but need to be careful about partially updating unifier)
        // perhaps Low and High (internal) props in conjunction? Or assume normalised ordering (which at the time of writing WE DONT DO)
        return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
    }

    private static bool TryUnify(Equivalence x, Equivalence y, IDictionary<VariableReference, Term> unifier)
    {
        // WOULD-BE-A-BUG-IF-THIS-WERE-PROD-CODE: Order shouldn't matter (but need to be careful about partially updating unifier)
        // perhaps Low and High (internal) props in conjunction?
        return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
    }

    ////private Sentence TryUnify(ExistentialQuantification x, ExistentialQuantification y, IDictionary<Variable, Term> unifier)
    ////{
    ////    var variable = ApplyToVariableDeclaration(existentialQuantification.Variable);
    ////    var sentence = ApplyToSentence(existentialQuantification.Sentence);
    ////    if (variable != existentialQuantification.Variable || sentence != existentialQuantification.Sentence)
    ////    {
    ////        return new ExistentialQuantification(variable, sentence);
    ////    }
    ////
    ////    return existentialQuantification;
    ////}

    private static bool TryUnify(Implication x, Implication y, IDictionary<VariableReference, Term> unifier)
    {
        return TryUnify(x.Antecedent, y.Antecedent, unifier) && TryUnify(x.Consequent, y.Consequent, unifier);
    }

    private static bool TryUnify(Negation x, Negation y, IDictionary<VariableReference, Term> unifier)
    {
        return TryUnify(x.Sentence, y.Sentence, unifier);
    }

    private static bool TryUnify(Predicate x, Predicate y, IDictionary<VariableReference, Term> unifier)
    {
        if (!x.Identifier.Equals(y.Identifier))
        {
            return false;
        }

        foreach (var args in x.Arguments.Zip(y.Arguments, (x, y) => (x, y)))
        {
            if (!TryUnify(args.x, args.y, unifier))
            {
                return false;
            }
        }

        return true;
    }

    ////private static bool TryUnify(UniversalQuantification x, UniversalQuantification y, IDictionary<Variable, Term> unifier)
    ////{
    ////    var variable = ApplyToVariableDeclaration(universalQuantification.Variable);
    ////    var sentence = ApplyToSentence(universalQuantification.Sentence);
    ////    if (variable != universalQuantification.Variable || sentence != universalQuantification.Sentence)
    ////    {
    ////        return new UniversalQuantification(variable, sentence);
    ////    }
    ////
    ////    return universalQuantification;
    ////}

    private static bool TryUnify(Term x, Term y, IDictionary<VariableReference, Term> unifier)
    {
        return (x, y) switch
        {
            (VariableReference variable, _) => TryUnify(variable, y, unifier),
            (_, VariableReference variable) => TryUnify(variable, x, unifier),
            (Function functionX, Function functionY) => TryUnify(functionX, functionY, unifier),
            _ => throw new ArgumentException("Null or unsupported type of Term encountered - cannot be unified"),
        };
    }

    private static bool TryUnify(VariableReference variable, Term other, IDictionary<VariableReference, Term> unifier)
    {
        if (unifier.TryGetValue(variable, out var value))
        {
            return TryUnify(value, other, unifier);
        }
        else if (other is VariableReference otherVariable && unifier.TryGetValue(otherVariable, out value))
        {
            return TryUnify(variable, value, unifier);
        }
        else if (Occurs(variable, other))
        {
            return false;
        }
        else
        {
            // This substitution is not in the book, but is so that e.g. Knows(John, X) and Knows(Y, Mother(Y)) will have x = Mother(John), not x = Mother(Y)
            other = new VariableSubstituter(unifier).ApplyTo(other);
            unifier[variable] = other;
            return true;
        }
    }

    private static bool TryUnify(Function x, Function y, IDictionary<VariableReference, Term> unifier)
    {
        if (!x.Identifier.Equals(y.Identifier))
        {
            return false;
        }

        foreach (var args in x.Arguments.Zip(y.Arguments, (x, y) => (x, y)))
        {
            if (!TryUnify(args.x, args.y, unifier))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Occurs(VariableReference variable, Term term)
    {
        // NB: This is very low-level, frequently called code.
        // Potentially avoidable GC pressure when creating a bunch of these.
        var finder = new VariableFinder(variable);
        finder.Visit(term);
        return finder.IsFound;
    }

    // NB: Doesn't stop as soon as IsFound is true.
    private class VariableFinder : RecursiveSentenceVisitor
    {
        private readonly VariableReference variableReference;

        public VariableFinder(VariableReference variableReference) => this.variableReference = variableReference;

        public bool IsFound { get; private set; } = false;

        public override void Visit(VariableReference variable)
        {
            if (variable.Equals(variableReference))
            {
                IsFound = true;
            }
        }
    }

    private class VariableSubstituter : RecursiveSentenceTransformation
    {
        private readonly IDictionary<VariableReference, Term> variableSubstitutions;

        public VariableSubstituter(IDictionary<VariableReference, Term> variableSubstitutions) => this.variableSubstitutions = variableSubstitutions;

        public override Term ApplyTo(VariableReference variable)
        {
            if (variableSubstitutions.TryGetValue(variable, out var substitutedTerm))
            {
                return substitutedTerm;
            }

            return variable;
        }
    }
}
