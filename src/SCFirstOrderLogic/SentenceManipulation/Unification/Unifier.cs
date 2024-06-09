﻿// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SCFirstOrderLogic.SentenceManipulation.Unification;

/// <summary>
/// <para>
/// Utility class for creating unifiers - that is, variable substitutions that yield the same result when applied each of the expressions that they unify.
/// </para>
/// <para>
/// This implementation includes an occurs check.
/// </para>
/// <para>
/// See §9.2.2 ("Unification") of 'Artificial Intelligence: A Modern Approach' for an explanation of this algorithm.
/// </para>
/// </summary>
public static class Unifier
{
    /// <summary>
    /// Attempts to create the most general unifier for two literals.
    /// </summary>
    /// <param name="x">One of the two literals to attempt to create a unifier for.</param>
    /// <param name="y">One of the two literals to attempt to create a unifier for.</param>
    /// <param name="unifier">If the literals can be unified, this out parameter will be the unifier.</param>
    /// <returns>True if the two literals can be unified, otherwise false.</returns>
    public static bool TryCreate(Literal x, Literal y, [MaybeNullWhen(returnValue: false)] out VariableSubstitution unifier)
    {
        var unifierAttempt = new MutableVariableSubstitution();

        if (TryUpdateInPlace(x, y, unifierAttempt))
        {
            unifier = unifierAttempt.ToReadOnly();
            return true;
        }

        unifier = null;
        return false;
    }

    /// <summary>
    /// Attempts to create the most general unifier for two literals.
    /// </summary>
    /// <param name="x">One of the two literals to attempt to create a unifier for.</param>
    /// <param name="y">One of the two literals to attempt to create a unifier for.</param>
    /// <returns>The unifier if the literals can be unified, otherwise null.</returns>
    public static VariableSubstitution? TryCreate(Literal x, Literal y)
    {
        var unifierAttempt = new MutableVariableSubstitution();
        return TryUpdateInPlace(x, y, unifierAttempt) ? unifierAttempt.ToReadOnly() : null;
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given literals.
    /// </summary>
    /// <param name="x">One of the two literals to attempt to unify.</param>
    /// <param name="y">One of the two literals to attempt to unify.</param>
    /// <param name="unifier">The unifier to update.</param>
    /// <param name="updatedUnifier">Will be populated with the updated unifier on success, or be null on failure.</param>
    /// <returns>True if the two literals can be unified, otherwise false.</returns>
    public static bool TryUpdate(Literal x, Literal y, VariableSubstitution unifier, [MaybeNullWhen(false)] out VariableSubstitution updatedUnifier)
    {
        var potentialUpdatedUnifier = unifier.ToMutable();

        if (TryUpdateInPlace(x, y, potentialUpdatedUnifier))
        {
            updatedUnifier = potentialUpdatedUnifier.ToReadOnly();
            return true;
        }

        updatedUnifier = null;
        return false;
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given literals.
    /// </summary>
    /// <param name="x">One of the two literals to attempt to unify.</param>
    /// <param name="y">One of the two literals to attempt to unify.</param>
    /// <param name="unifier">The unifier to update. Will be updated to refer to a new unifier on success, or be unchanged on failure.</param>
    /// <returns>True if the two literals can be unified, otherwise false.</returns>
    public static bool TryUpdate(Literal x, Literal y, ref VariableSubstitution unifier)
    {
        var updatedUnifier = unifier.ToMutable();

        if (TryUpdateInPlace(x, y, updatedUnifier))
        {
            unifier = updatedUnifier.ToReadOnly();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given literals.
    /// </summary>
    /// <param name="x">One of the two literals to attempt to unify.</param>
    /// <param name="y">One of the two literals to attempt to unify.</param>
    /// <param name="unifier">The unifier to update.</param>
    /// <returns>The unifier if the literals can be unified, otherwise null.</returns>
    public static VariableSubstitution? TryUpdate(Literal x, Literal y, VariableSubstitution unifier)
    {
        var unifierAttempt = unifier.ToMutable();
        return TryUpdateInPlace(x, y, unifierAttempt) ? unifierAttempt.ToReadOnly() : null;
    }

    /// <summary>
    /// Attempts to create the most general unifier for two predicates.
    /// </summary>
    /// <param name="x">One of the two predicates to attempt to create a unifier for.</param>
    /// <param name="y">One of the two predicates to attempt to create a unifier for.</param>
    /// <param name="unifier">If the predicates can be unified, this out parameter will be the unifier.</param>
    /// <returns>True if the two predicates can be unified, otherwise false.</returns>
    public static bool TryCreate(Predicate x, Predicate y, [MaybeNullWhen(returnValue: false)] out VariableSubstitution unifier)
    {
        return TryCreate((Literal)x, (Literal)y, out unifier);
    }

    /// <summary>
    /// Attempts to create the most general unifier for two predicates.
    /// </summary>
    /// <param name="x">One of the two predicates to attempt to create a unifier for.</param>
    /// <param name="y">One of the two predicates to attempt to create a unifier for.</param>
    /// <returns>The unifier if the predicates can be unified, otherwise null.</returns>
    public static VariableSubstitution? TryCreate(Predicate x, Predicate y)
    {
        return TryCreate((Literal)x, (Literal)y);
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given predicates.
    /// </summary>
    /// <param name="x">One of the two predicates to attempt to unify.</param>
    /// <param name="y">One of the two predicates to attempt to unify.</param>
    /// <param name="unifier">The unifier to update.</param>
    /// <param name="updatedUnifier">Will be populated with the updated unifier on success, or be null on failure.</param>
    /// <returns>True if the two predicates can be unified, otherwise false.</returns>
    public static bool TryUpdate(Predicate x, Predicate y, VariableSubstitution unifier, [MaybeNullWhen(false)] out VariableSubstitution updatedUnifier)
    {
        return TryUpdate((Literal)x, (Literal)y, unifier, out updatedUnifier);
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given predicates.
    /// </summary>
    /// <param name="x">One of the two predicates to attempt to unify.</param>
    /// <param name="y">One of the two predicates to attempt to unify.</param>
    /// <param name="unifier">The unifier to update. Will be updated to refer to a new unifier on success, or be unchanged on failure.</param>
    /// <returns>True if the two predicates can be unified, otherwise false.</returns>
    public static bool TryUpdate(Predicate x, Predicate y, ref VariableSubstitution unifier)
    {
        return TryUpdate((Literal)x, (Literal)y, ref unifier);
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given predicates.
    /// </summary>
    /// <param name="x">One of the two predicates to attempt to unify.</param>
    /// <param name="y">One of the two predicates to attempt to unify.</param>
    /// <param name="unifier">The unifier to update.</param>
    /// <returns>The unifier if the predicates can be unified, otherwise null.</returns>
    public static VariableSubstitution? TryUpdate(Predicate x, Predicate y, VariableSubstitution unifier)
    {
        return TryUpdate((Literal)x, (Literal)y, unifier);
    }

    /// <summary>
    /// Attempts to create the most general unifier for two terms.
    /// </summary>
    /// <param name="x">One of the two terms to attempt to create a unifier for.</param>
    /// <param name="y">One of the two terms to attempt to create a unifier for.</param>
    /// <param name="unifier">If the terms can be unified, this out parameter will be the unifier.</param>
    /// <returns>True if the two terms can be unified, otherwise false.</returns>
    public static bool TryCreate(Term x, Term y, [MaybeNullWhen(false)] out VariableSubstitution unifier)
    {
        var unifierAttempt = new MutableVariableSubstitution();

        if (TryUpdateInPlace(x, y, unifierAttempt))
        {
            unifier = unifierAttempt.ToReadOnly();
            return true;
        }

        unifier = null;
        return false;
    }

    /// <summary>
    /// Attempts to create the most general unifier for two terms.
    /// </summary>
    /// <param name="x">One of the two terms to attempt to create a unifier for.</param>
    /// <param name="y">One of the two terms to attempt to create a unifier for.</param>
    /// <returns>The unifier if the terms can be unified, otherwise null.</returns>
    public static VariableSubstitution? TryCreate(Term x, Term y)
    {
        var unifierAttempt = new MutableVariableSubstitution();
        return TryUpdateInPlace(x, y, unifierAttempt) ? unifierAttempt.ToReadOnly() : null;
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given terms.
    /// </summary>
    /// <param name="x">One of the two terms to attempt to unify.</param>
    /// <param name="y">One of the two terms to attempt to unify.</param>
    /// <param name="unifier">The unifier to update.</param>
    /// <param name="updatedUnifier">Will be populated with the updated unifier on success, or be null on failure.</param>
    /// <returns>True if the two terms can be unified, otherwise false.</returns>
    public static bool TryUpdate(Term x, Term y, VariableSubstitution unifier, [MaybeNullWhen(false)] out VariableSubstitution updatedUnifier)
    {
        var potentialUpdatedUnifier = unifier.ToMutable();

        if (TryUpdateInPlace(x, y, potentialUpdatedUnifier))
        {
            updatedUnifier = potentialUpdatedUnifier.ToReadOnly();
            return true;
        }

        updatedUnifier = null;
        return false;
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given terms.
    /// </summary>
    /// <param name="x">One of the two terms to attempt to unify.</param>
    /// <param name="y">One of the two terms to attempt to unify.</param>
    /// <param name="unifier">The unifier to update. Will be updated to refer to a new unifier on success, or be unchanged on failure.</param>
    /// <returns>True if the two terms can be unified, otherwise false.</returns>
    public static bool TryUpdate(Term x, Term y, ref VariableSubstitution unifier)
    {
        var updatedUnifier = unifier.ToMutable();

        if (TryUpdateInPlace(x, y, updatedUnifier))
        {
            unifier = updatedUnifier.ToReadOnly();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to update a unifier so that it (also) unifies two given terms.
    /// </summary>
    /// <param name="x">One of the two terms to attempt to unify.</param>
    /// <param name="y">One of the two terms to attempt to unify.</param>
    /// <param name="unifier">The unifier to update.</param>
    /// <returns>The unifier if the literals can be unified, otherwise null.</returns>
    public static VariableSubstitution? TryUpdate(Term x, Term y, VariableSubstitution unifier)
    {
        var unifierAttempt = unifier.ToMutable();
        return TryUpdateInPlace(x, y, unifierAttempt) ? unifierAttempt.ToReadOnly() : null;
    }

    private static bool TryUpdateInPlace(Literal x, Literal y, MutableVariableSubstitution unifier)
    {
        if (x.IsNegated != y.IsNegated 
            || !x.Predicate.Identifier.Equals(y.Predicate.Identifier)
            || x.Predicate.Arguments.Count != y.Predicate.Arguments.Count)
        {
            return false;
        }

        foreach (var args in x.Predicate.Arguments.Zip(y.Predicate.Arguments, (x, y) => (x, y)))
        {
            if (!TryUpdateInPlace(args.x, args.y, unifier))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryUpdateInPlace(Term x, Term y, MutableVariableSubstitution unifier)
    {
        return (x, y) switch
        {
            (VariableReference variable, _) => TryUpdateInPlace(variable, y, unifier),
            (_, VariableReference variable) => TryUpdateInPlace(variable, x, unifier),
            (Function functionX, Function functionY) => TryUpdateInPlace(functionX, functionY, unifier),
            // Below, the only potential for equality is if they're both constants. Perhaps worth testing this
            // versus that explicitly and a default that just returns false. Similar from a performance
            // perspective.
            _ => x.Equals(y),
        };
    }

    private static bool TryUpdateInPlace(VariableReference variable, Term other, MutableVariableSubstitution unifier)
    {
        if (variable.Equals(other))
        {
            return true;
        }
        else if (unifier.Bindings.TryGetValue(variable, out var variableValue))
        {
            // The variable is already mapped to something - we need to make sure that the
            // mapping is consistent with the "other" value.
            return TryUpdateInPlace(variableValue, other, unifier);
        }
        else if (other is VariableReference otherVariable && unifier.Bindings.TryGetValue(otherVariable, out var otherVariableValue))
        {
            // The other value is also a variable that is already mapped to something - we need to make sure that the
            // mapping is consistent with the "other" value.
            return TryUpdateInPlace(variable, otherVariableValue, unifier);
        }
        else if (Occurs(variable, unifier.ApplyTo(other)))
        {
            // We'd be adding a cycle if we added this binding - so fail instead.
            return false;
        }
        else
        {
            unifier.AddBinding(variable, other);
            return true;
        }
    }

    private static bool TryUpdateInPlace(Function x, Function y, MutableVariableSubstitution unifier)
    {
        if (!x.Identifier.Equals(y.Identifier) || x.Arguments.Count != y.Arguments.Count)
        {
            return false;
        }

        for (int i = 0; i < x.Arguments.Count; i++)
        {
            if (!TryUpdateInPlace(x.Arguments[i], y.Arguments[i], unifier))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Occurs(VariableReference variableReference, Term term)
    {
        return term switch
        {
            Constant c => false,
            VariableReference v => variableReference.Equals(v),
            Function f => f.Arguments.Any(a => Occurs(variableReference, a)),
            _ => throw new ArgumentException($"Unexpected term type '{term.GetType()}' encountered", nameof(term)),
        };
    }
}
