// Copyright © 2023-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
/// <typeparam name="TFeature">The type of each key of the feature vectors.</typeparam>
public class AsyncFeatureVectorIndex<TFeature> : IAsyncEnumerable<CNFClause>
    where TFeature : notnull
{
    /// <summary>
    /// The inner <see cref="CNFClause"/>-valued index that this one merely wraps.
    /// </summary>
    private readonly AsyncFeatureVectorIndex<TFeature, CNFClause> innerIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncFeatureVectorIndex{TFeature}"/> class.
    /// </summary>
    /// <param name="featureVectorSelector">The delegate to use to retrieve the feature vector for any given clause.</param>
    /// <param name="root">The root node of the index.</param>
    public AsyncFeatureVectorIndex(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
        IAsyncFeatureVectorIndexNode<TFeature, CNFClause> root)
    {
        innerIndex = new(featureVectorSelector, root);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncFeatureVectorIndex{TFeature}"/> class, 
    /// and adds some additional initial content (beyond any already attached to the provided root node).
    /// </summary>
    /// <param name="featureVectorSelector">The delegate to use to retrieve the feature vector for any given clause.</param>
    /// <param name="root">The root node of the tree.</param>
    /// <param name="content">The additional content to be added.</param>
    public AsyncFeatureVectorIndex(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
        IAsyncFeatureVectorIndexNode<TFeature, CNFClause> root,
        IEnumerable<CNFClause> content)
    {
        innerIndex = new(featureVectorSelector, root, content.Select(a => KeyValuePair.Create(a, a)));
    }

    /// <summary>
    /// Adds a clause to the index.
    /// </summary>
    /// <param name="key">The clause to add.</param>
    /// <returns>A task representing completion of the operation.</returns>
    public Task AddAsync(CNFClause key) => innerIndex.AddAsync(key, key);

    /// <summary>
    /// Removes a clause from the index.
    /// </summary>
    /// <param name="key">The clause to remove.</param>
    /// <returns>A value indicating whether the clause was present prior to this operation.</returns>
    public Task<bool> RemoveAsync(CNFClause key) => innerIndex.RemoveAsync(key);

    /// <summary>
    /// Determines whether a given clause (matched exactly) is present in the index.
    /// </summary>
    /// <param name="key">The clause to check for.</param>
    /// <returns>True if and only if the clause is present in the index.</returns>
    public async Task<bool> ContainsAsync(CNFClause key) => (await innerIndex.TryGetAsync(key)).isSucceeded;

    /// <summary>
    /// Returns an enumerable of each stored clause that subsumes a given clause.
    /// </summary>
    /// <param name="clause">The stored clauses that subsume this clause will be retrieved.</param>
    /// <returns>An async enumerable each stored clause that subsumes the given clause.</returns>
    public IAsyncEnumerable<CNFClause> GetSubsuming(CNFClause clause) => innerIndex.GetSubsuming(clause);

    /// <summary>
    /// Returns an enumerable of each stored clause that is subsumed by a given clause.
    /// </summary>
    /// <param name="clause">The stored clauses that are subsumed by this clause will be retrieved.</param>
    /// <returns>An async enumerable of each clause that is subsumed by the given clause.</returns>
    public IAsyncEnumerable<CNFClause> GetSubsumed(CNFClause clause) => innerIndex.GetSubsumed(clause);

    /// <inheritdoc />
    public async IAsyncEnumerator<CNFClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var (_, value) in innerIndex.WithCancellation(cancellationToken))
        {
            yield return value;
        }
    }
}
