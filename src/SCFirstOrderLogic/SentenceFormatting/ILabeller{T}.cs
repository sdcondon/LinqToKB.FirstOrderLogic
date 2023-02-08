﻿namespace SCFirstOrderLogic.SentenceFormatting
{
    /// <summary>
    /// Interface for types capable of creating labels for symbols that are unique (within a specified scope).
    /// </summary>
    /// <typeparam name="T">The type of symbols to be labelled.</typeparam>
    public interface ILabeller<T>
    {
        /// <summary>
        /// Create a new <see cref="ILabellingScope{T}"/>.
        /// </summary>
        /// <returns>A new labelling scope.</returns>
        ILabellingScope<T> MakeLabellingScope();
    }
}