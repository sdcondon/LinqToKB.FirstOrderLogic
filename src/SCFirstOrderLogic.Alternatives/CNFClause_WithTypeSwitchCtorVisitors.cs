﻿// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic;
using SCFirstOrderLogic.SentenceFormatting;
using SCFirstOrderLogic.SentenceManipulation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SCFirstOrderLogic;

/// <summary>
/// Representation of an individual clause (i.e. a disjunction of <see cref="Literal"/>s) of a first-order logic sentence in conjunctive normal form.
/// </summary>
public class CNFClause_WithTypeSwitchCtorVisitors : IEquatable<CNFClause_WithTypeSwitchCtorVisitors>
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AltCNFClause_WithTypeSwitch"/> class from a sentence that is a disjunction of literals (a literal being a predicate or a negated predicate).
    /// </summary>
    /// <param name="sentence">The clause, represented as a <see cref="Sentence"/>. An <see cref="ArgumentException"/> exception will be thrown if it is not a disjunction of literals.</param>
    public CNFClause_WithTypeSwitchCtorVisitors(Sentence sentence)
    {
        var ctor = new ClauseConstructor();
        ctor.Visit(sentence);

        // We *could* actually use an immutable type to stop unscrupulous users from making it mutable by casting, but
        // its a super low-level class and I'd rather err on the side of using the smallest & simplest implementation possible.
        // Note that we order literals - which is important to justifiably consider the clause "normalised".
        // WOULD-BE-A-BUG-IF-THIS-WERE-PROD-CODE: Possible problems when hash code collisions occur. Probably worth a more robust approach at some point - but
        // clause equality will be checked a LOT during resolution..
        Literals = ctor.Literals.OrderBy(l => l.GetHashCode()).ToArray();
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="CNFClause_WithTypeSwitchCtorVisitors"/> class from an enumerable of literals (removing any mutually-negating literals and duplicates as it does so).
    /// </summary>
    /// <param name="literals">The set of literals to be included in the clause.</param>
    public CNFClause_WithTypeSwitchCtorVisitors(IEnumerable<Literal_WithTypeSwitchCtorVisitors> literals)
    {
        // We *could* actually use an immutable type to stop unscrupulous users from making it mutable by casting, but
        // its a super low-level class and I'd rather err on the side of using the simplest/smallest implementation possible.
        // Note that we order literals - important to justifiably consider the clause "normalised".
        Literals = literals.OrderBy(l => l.GetHashCode()).ToArray();
    }

    /// <summary>
    /// Gets an instance of the empty clause.
    /// </summary>
    public static CNFClause_WithTypeSwitchCtorVisitors Empty { get; } = new CNFClause_WithTypeSwitchCtorVisitors(Array.Empty<Literal_WithTypeSwitchCtorVisitors>());

    /// <summary>
    /// Gets the collection of literals that comprise this clause.
    /// </summary>
    public IReadOnlyCollection<Literal_WithTypeSwitchCtorVisitors> Literals { get; }

    /// <summary>
    /// <para>
    /// Gets a value indicating whether this is a Horn clause - that is, whether at most one of its literals is positive.
    /// </para>
    /// <para>
    /// No caching here, but the class is immutable so recalculating every time is wasted effort, strictly speaking.
    /// Various things we could do, but for the moment I'm erring on the side of doing nothing, on the grounds that these
    /// properties are unlikely to be on the "hot" path of any given applicaiton.
    /// </para>
    /// </summary>
    public bool IsHornClause => Literals.Count(l => l.IsPositive) <= 1;

    /// <summary>
    /// Gets a value indicating whether this is a definite clause - that is, whether exactly one of its literals is positive.
    /// </summary>
    public bool IsDefiniteClause => Literals.Count(l => l.IsPositive) == 1;

    /// <summary>
    /// Gets a value indicating whether this is a goal clause - that is, whether none of its literals is positive.
    /// </summary>
    public bool IsGoalClause => !Literals.Any(l => l.IsPositive);

    /// <summary>
    /// Gets a value indicating whether this is a unit clause - that is, whether it contains exactly one literal.
    /// </summary>
    public bool IsUnitClause => Literals.Count == 1;

    /// <summary>
    /// Gets a value indicating whether this is an empty clause (that implicitly evaluates to false). Can occur as a result of resolution.
    /// </summary>
    public bool IsEmpty => Literals.Count == 0;

    /// <summary>
    /// <para>
    /// Returns a string that represents the current object.
    /// </para>
    /// <para>
    /// NB: The implementation of this override creates a <see cref="SentenceFormatter"/> object and uses it to format the clause.
    /// Note that this will not guarantee unique labelling of normalisation terms (standardised variables or Skolem functions)
    /// across multiple calls, or provide any choice as to the sets of labels used for normalisation terms. If you want either
    /// of these things, instantiate your own <see cref="SentenceFormatter"/> instance.
    /// </para>
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    ////public override string ToString() => new SentenceFormatter().Print(this);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CNFClause_WithTypeSwitchCtorVisitors clause && Equals(clause);

    /// <inheritdoc />
    /// <remarks>
    /// Clauses that contain exactly the same collection of literals are considered equal.
    /// </remarks>
    public bool Equals(CNFClause_WithTypeSwitchCtorVisitors? other)
    {
        if (other == null || Literals.Count != other.Literals.Count)
        {
            return false;
        }

        foreach (var (xLiteral, yLiteral) in Literals.Zip(other.Literals, (x, y) => (x, y)))
        {
            if (!xLiteral.Equals(yLiteral))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Clauses that contain exactly the same collection of literals in the same order are considered equal.
    /// </remarks>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var literal in Literals)
        {
            hash.Add(literal);
        }

        return hash.ToHashCode();
    }

    private class ClauseConstructor : RecursiveSentenceVisitor_WithTypeSwitch
    {
        public HashSet<Literal_WithTypeSwitchCtorVisitors> Literals { get; } = new();

        public override void Visit(Sentence sentence)
        {
            if (sentence is Disjunction disjunction)
            {
                // The sentence is assumed to be a clause (i.e. a disjunction of literals) - so just skip past all the disjunctions at the root.
                base.Visit(disjunction);
            }
            else
            {
                // Assume we've hit a literal. NB will throw if its not actually a literal.
                // Afterwards, we don't need to look any further down the tree for the purposes of this class (though the CNFLiteral ctor that
                // we invoke here does so to figure out the details of the literal). So we can just return rather than invoking base.Visit.
                Literals.Add(new Literal_WithTypeSwitchCtorVisitors(sentence));
            }
        }
    }
}
