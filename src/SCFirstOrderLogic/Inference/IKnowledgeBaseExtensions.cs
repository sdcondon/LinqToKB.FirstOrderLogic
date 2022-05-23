﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference
{
    /// <summary>
    /// Helpful extension methods for <see cref="IKnowledgeBase"/> instances.
    /// </summary>
    public static class IKnowledgeBaseExtensions
    {
        /// <summary>
        /// Inform a knowledge base that a given enumerable of sentences can all be assumed to hold true when answering queries.
        /// </summary>
        /// <param name="knowledgeBase">The knowledge base to tell.</param>
        /// <param name="sentences">The sentences that can be assumed to hold true when answering queries.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        public static async Task TellAsync(this IKnowledgeBase knowledgeBase, IEnumerable<Sentence> sentences, CancellationToken cancellationToken = default)
        {
            foreach (var sentence in sentences)
            {
                await knowledgeBase.TellAsync(sentence, cancellationToken); // No guarantee of thread safety - so go one at a time. TODO: this is dumb - not our responsibility
            }
        }

        /// <summary>
        /// Asks the knowledge base if a given sentence necessarily holds true, given what it knows.
        /// </summary>
        /// <param name="knowledgeBase">The knowledge base to ask.</param>
        /// <param name="sentence">The query sentence.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>True if the sentence is known to be true, false if it is known to be false or cannot be determined.</returns>
        public static async Task<bool> AskAsync(this IKnowledgeBase knowledgeBase, Sentence sentence, CancellationToken cancellationToken = default)
        {
            using var query = await knowledgeBase.CreateQueryAsync(sentence, cancellationToken);
            return await query.CompleteAsync(cancellationToken);
        }
    }
}