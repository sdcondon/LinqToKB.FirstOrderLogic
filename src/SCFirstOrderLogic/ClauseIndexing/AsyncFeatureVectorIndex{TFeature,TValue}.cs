// Copyright © 2023-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.InternalUtilities;
using SCFirstOrderLogic.SentenceManipulation.VariableManipulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.ClauseIndexing;

/// <summary>
/// <para>
/// An implementation of a feature vector index for <see cref="CNFClause"/>s.
/// </para>
/// <para>
/// Feature vector indexing (in this context, at least) is an indexing method for clause subsumption.
/// That is, feature vector indices can be used to store clauses in such a way that we can quickly look up the stored clauses that subsume or are subsumed by a query clause.
/// </para>
/// </summary>
/// <typeparam name="TFeature">The type of the keys of the feature vectors.</typeparam>
/// <typeparam name="TValue">The type of the value associated with each stored clause.</typeparam>
// todo-breaking-performance: at least on the read side, consider processing nodes in parallel.
// what are some best practices here (esp re consumers/node implementers being able to control DoP)?
// e.g allow consumers to pass a scheduler? allow nodes to specify a scheduler?
public class AsyncFeatureVectorIndex<TFeature, TValue> : IAsyncEnumerable<KeyValuePair<TFeature, TValue>>
    where TFeature : notnull
{
    /// <summary>
    /// The delegate used to retrieve the feature vector for any given clause.
    /// </summary>
    private readonly Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector;

    /// <summary>
    /// The root node of the index.
    /// </summary>
    private readonly IAsyncFeatureVectorIndexNode<TFeature, TValue> root;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncFeatureVectorIndex{TFeature,TValue}"/> class.
    /// </summary>
    /// <param name="featureVectorSelector">The delegate to use to retrieve the feature vector for any given clause.</param>
    /// <param name="root">The root node of the index.</param>
    public AsyncFeatureVectorIndex(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
        IAsyncFeatureVectorIndexNode<TFeature, TValue> root)
        : this(featureVectorSelector, root, Enumerable.Empty<KeyValuePair<CNFClause, TValue>>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncFeatureVectorIndex{TFeature,TValue}"/> class,
    /// and adds some additional initial content (beyond any already attached to the provided root node).
    /// </summary>
    /// <param name="featureVectorSelector">The delegate to use to retrieve the feature vector for any given clause.</param>
    /// <param name="root">The root node of the index.</param>
    /// <param name="content">The additional content to be added.</param>
    public AsyncFeatureVectorIndex(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
        IAsyncFeatureVectorIndexNode<TFeature, TValue> root,
        IEnumerable<KeyValuePair<CNFClause, TValue>> content)
    {
        ArgumentNullException.ThrowIfNull(featureVectorSelector);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(content);

        this.featureVectorSelector = featureVectorSelector;
        this.root = root;

        foreach (var kvp in content)
        {
            AddAsync(kvp.Key, kvp.Value).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Adds a clause and associated value to the index.
    /// </summary>
    /// <param name="key">The clause to add.</param>
    /// <param name="value">The value to associate with the clause.</param>
    public async Task AddAsync(CNFClause key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key == CNFClause.Empty)
        {
            throw new ArgumentException("The empty clause is not a valid key", nameof(key));
        }

        var currentNode = root;
        foreach (var vectorComponent in MakeAndSortFeatureVector(key))
        {
            currentNode = await currentNode.GetOrAddChildAsync(vectorComponent);
        }

        await currentNode.AddValueAsync(key, value);
    }

    /// <summary>
    /// Removes a clause from the index.
    /// </summary>
    /// <param name="key">The clause to remove.</param>
    /// <returns>A value indicating whether the clause was present prior to this operation.</returns>
    public async Task<bool> RemoveAsync(CNFClause key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var featureVector = MakeAndSortFeatureVector(key);

        return await ExpandNodeAsync(root, 0);

        async ValueTask<bool> ExpandNodeAsync(IAsyncFeatureVectorIndexNode<TFeature, TValue> node, int componentIndex)
        {
            if (componentIndex < featureVector.Count)
            {
                var component = featureVector[componentIndex];
                var childNode = await node.TryGetChildAsync(component);

                if (childNode == null || !await ExpandNodeAsync(childNode, componentIndex + 1))
                {
                    return false;
                }

                if (!await childNode.ChildrenAscending.AnyAsync() && !await childNode.KeyValuePairs.AnyAsync())
                {
                    await node.DeleteChildAsync(component);
                }

                return true;
            }
            else
            {
                return await node.RemoveValueAsync(key);
            }
        }
    }

    /// <summary>
    /// Attempts to retrieve the value associated with a clause.
    /// </summary>
    /// <param name="key">The clause to retrieve the associated value of.</param>
    /// <returns>A task that returns a value indicating whether it was successful, and if so what the retrieved value is.</returns>
    public async Task<(bool isSucceeded, TValue? value)> TryGetAsync(CNFClause key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var currentNode = root;
        foreach (var element in MakeAndSortFeatureVector(key))
        {
            var childNode = await currentNode.TryGetChildAsync(element);
            if (childNode != null)
            {
                currentNode = childNode;
            }
            else
            {
                return (false, default);
            }
        }

        return await currentNode.TryGetValueAsync(key);
    }

    /// <summary>
    /// Returns an enumerable of the values associated with each stored clause that subsumes a given clause.
    /// </summary>
    /// <param name="clause">The values associated with the stored clauses that subsume this clause will be retrieved.</param>
    /// <returns>An async enumerable of the values associated with each stored clause that subsumes the given clause.</returns>
    public IAsyncEnumerable<TValue> GetSubsuming(CNFClause clause)
    {
        ArgumentNullException.ThrowIfNull(clause);

        var featureVector = MakeAndSortFeatureVector(clause);

        return ExpandNode(root, 0);

        async IAsyncEnumerable<TValue> ExpandNode(IAsyncFeatureVectorIndexNode<TFeature, TValue> node, int componentIndex)
        {
            if (componentIndex < featureVector.Count)
            {
                var component = featureVector[componentIndex];

                // Recurse for children with matching feature and lower magnitude:
                var matchingChildNodes = node
                    .ChildrenAscending
                    .SkipWhile(kvp => node.FeatureComparer.Compare(kvp.Key.Feature, component.Feature) < 0)
                    .TakeWhile(kvp => node.FeatureComparer.Compare(kvp.Key.Feature, component.Feature) == 0 && kvp.Key.Magnitude <= component.Magnitude)
                    .Select(kvp => kvp.Value);

                await foreach (var childNode in matchingChildNodes)
                {
                    await foreach (var value in ExpandNode(childNode, componentIndex + 1))
                    {
                        yield return value;
                    }
                }

                // Matching feature might not be there at all in stored clauses, which means it has an implicit
                // magnitude of zero, and we thus can't preclude subsumption - so we also just skip the current key element:
                await foreach (var value in ExpandNode(node, componentIndex + 1))
                {
                    yield return value;
                }
            }
            else
            {
                // NB: note that we need to filter the values to those keyed by clauses that actually
                // subsume the query clause. The node values are just the *candidate* set.
                await foreach (var value in node.KeyValuePairs.Where(kvp => kvp.Key.Subsumes(clause)).Select(kvp => kvp.Value))
                {
                    yield return value;
                }
            }
        }
    }

    /// <summary>
    /// Returns an enumerable of the values associated with each stored clause that is subsumed by a given clause.
    /// </summary>
    /// <param name="clause">The values associated with the stored clauses that are subsumed by this clause will be retrieved.</param>
    /// <returns>An async enumerable of the values associated with each stored clause that is subsumed by the given clause.</returns>
    public IAsyncEnumerable<TValue> GetSubsumed(CNFClause clause)
    {
        ArgumentNullException.ThrowIfNull(clause);

        var featureVector = MakeAndSortFeatureVector(clause);

        return ExpandNode(root, 0);

        async IAsyncEnumerable<TValue> ExpandNode(IAsyncFeatureVectorIndexNode<TFeature, TValue> node, int componentIndex)
        {
            if (componentIndex < featureVector.Count)
            {
                var component = featureVector[componentIndex];

                // NB: only need to compare feature (not magnitude) here because the only way that component index could be greater
                // than 0 is if all earlier components matched to an ancestor node by feature (which had an equal or higher magnitude).
                // And there shouldn't be any duplicate features in the path from root to leaf - so only need to look at feature here.
                var matchingChildNodes = componentIndex == 0
                    ? node.ChildrenDescending
                    : node.ChildrenDescending.TakeWhile(kvp => root.FeatureComparer.Compare(kvp.Key.Feature, featureVector[componentIndex - 1].Feature) > 0);

                await foreach (var ((childFeature, childMagnitude), childNode) in matchingChildNodes)
                {
                    var childFeatureVsCurrent = root.FeatureComparer.Compare(childFeature, component.Feature);

                    if (childFeatureVsCurrent <= 0)
                    {
                        var componentIndexOffset = childFeatureVsCurrent == 0 && childMagnitude >= component.Magnitude ? 1 : 0;

                        await foreach (var value in ExpandNode(childNode, componentIndex + componentIndexOffset))
                        {
                            yield return value;
                        }
                    }
                }
            }
            else
            {
                await foreach (var value in GetAllDescendentValues(node))
                {
                    yield return value;
                }
            }
        }

        async IAsyncEnumerable<TValue> GetAllDescendentValues(IAsyncFeatureVectorIndexNode<TFeature, TValue> node)
        {
            // NB: note that we need to filter the values to those keyed by clauses that are
            // actually subsumed by the query clause. The node values are just the *candidate* set.
            await foreach (var value in node.KeyValuePairs.Where(kvp => clause.Subsumes(kvp.Key)).Select(kvp => kvp.Value))
            {
                yield return value;
            }

            await foreach (var (_, childNode) in node.ChildrenAscending)
            {
                await foreach (var value in GetAllDescendentValues(childNode))
                {
                    yield return value;
                }
            }
        }
    }

    public async IAsyncEnumerator<KeyValuePair<TFeature, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the feature vector for a clause, and sorts it using the feature comparer specified by the index's root node.
    /// </summary>
    /// <param name="clause">The clause to retrieve the feature vector for.</param>
    /// <returns>The feature vector, represented as a read-only list.</returns>
    private IReadOnlyList<FeatureVectorComponent<TFeature>> MakeAndSortFeatureVector(CNFClause clause)
    {
        // todo-performance: if we need a list anyway, probably faster to make the list, then sort it in place? test me
        // todo-robustness: should probably throw if any distinct pairs have a comparison of zero. could happen efficiently as part of the sort
        return featureVectorSelector(clause).OrderBy(kvp => kvp.Feature, root.FeatureComparer).ToList();
    }
}
