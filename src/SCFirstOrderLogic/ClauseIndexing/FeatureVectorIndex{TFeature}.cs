// Copyright © 2023-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SCFirstOrderLogic.ClauseIndexing;

/// <summary>
/// <para>
/// An implementation of a feature vector index for <see cref="CNFClause"/>s. Specifically, one for which the associated values are the clauses themselves.
/// </para>
/// <para>
/// Feature vector indexing (in this context, at least) is an indexing method for clause subsumption.
/// That is, feature vector indices can be used to store clauses in such a way that we can quickly look up the stored clauses that subsume or are subsumed by a query clause.
/// </para>
/// </summary>
/// <typeparam name="TFeature">The type of the keys of the feature vectors.</typeparam>
public class FeatureVectorIndex<TFeature> : IEnumerable<CNFClause>
    where TFeature : notnull
{
    /// <summary>
    /// The inner <see cref="CNFClause"/>-valued index that this one merely wraps.
    /// </summary>
    private readonly FeatureVectorIndex<TFeature, CNFClause> innerIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureVectorIndex{TFeature}"/> class.
    /// </summary>
    /// <param name="featureVectorSelector">The delegate to use to retrieve the feature vector for any given clause.</param>
    /// <param name="root">The root node of the index.</param>
    public FeatureVectorIndex(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
        IFeatureVectorIndexNode<TFeature, CNFClause> root)
    {
        innerIndex = new(featureVectorSelector, root);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureVectorIndex{TFeature}"/> class with
    /// some additional initial content (beyond any already attached to the provided root node).
    /// </summary>
    /// <param name="featureVectorSelector">The delegate to use to retrieve the feature vector for any given clause.</param>
    /// <param name="root">The root node of the tree.</param>
    /// <param name="content">The additional content to be added.</param>
    public FeatureVectorIndex(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
        IFeatureVectorIndexNode<TFeature, CNFClause> root,
        IEnumerable<CNFClause> content)
    {
        innerIndex = new(featureVectorSelector, root, content.Select(t => KeyValuePair.Create(t, t)));
    }

    /// <summary>
    /// Adds a clause to the index.
    /// </summary>
    /// <param name="key">The clause to add.</param>
    public void Add(CNFClause key)
    {
        innerIndex.Add(key, key);
    }

    /// <summary>
    /// Removes a clause from the index.
    /// </summary>
    /// <param name="key">The clause to remove.</param>
    /// <returns>A value indicating whether the clause was present prior to this operation.</returns>
    public bool Remove(CNFClause key)
    {
        return innerIndex.Remove(key);
    }

    /// <summary>
    /// If the index contains any clause that subsumes the given clause, does nothing and returns <see langword="false"/>.
    /// Otherwise, adds the given clause to the index, removes any clauses that it subsumes, and returns <see langword="true"/>.
    /// </summary>
    /// <param name="clause">The clause to add.</param>
    /// <returns>True if and only if the clause was added.</returns>
    public bool TryReplaceSubsumed(CNFClause clause)
    {
        return innerIndex.TryReplaceSubsumed(clause, clause);
    }

    /// <summary>
    /// Determines whether a given clause (matched exactly) is present in the index.
    /// </summary>
    /// <param name="key">The clause to check for.</param>
    /// <returns>True if and only if the clause is present in the index.</returns>
    public bool Contains(CNFClause key) => innerIndex.TryGet(key, out _);

    /// <summary>
    /// Returns an enumerable of each stored clause that subsumes a given clause.
    /// </summary>
    /// <param name="clause">The stored clauses that subsume this clause will be retrieved.</param>
    /// <returns>An enumerable each stored clause that subsumes the given clause.</returns>
    public IEnumerable<CNFClause> GetSubsuming(CNFClause clause)
    {
        return innerIndex.GetSubsuming(clause);
    }

    /// <summary>
    /// Returns an enumerable of each stored clause that is subsumed by a given clause.
    /// </summary>
    /// <param name="clause">The stored clauses that are subsumed by this clause will be retrieved.</param>
    /// <returns>An enumerable of each stored clause that is subsumed by the given clause.</returns>
    public IEnumerable<CNFClause> GetSubsumed(CNFClause clause)
    {
        return innerIndex.GetSubsumed(clause);
    }

    /// <inheritdoc />>
    public IEnumerator<CNFClause> GetEnumerator()
    {
        foreach (var (_, value) in innerIndex)
        {
            yield return value;
        }
    }

    /// <inheritdoc />>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
