﻿using System.Collections.Generic;

namespace SCFirstOrderLogic.SentenceFormatting
{
    /// <summary>
    /// Sets of labels to use for things, intended to be useful for providing to <see cref="LabelSetLabeller{T}"/> instances.
    /// </summary>
    public static class LabelSets
    {
        /// <summary>
        /// The (lowercase) Greek alphabet.
        /// </summary>
        public static readonly IEnumerable<string> LowerGreekAlphabet = new[] { "α", "β", "γ", "δ", "ε", "ζ", "η", "θ", "ι", "κ", "λ", "μ", "ν", "ξ", "ο", "π", "ρ", "σ", "τ", "υ", "φ", "χ", "ψ", "ω" };

        /// <summary>
        /// The (lower-case) modern Latin alphabet
        /// </summary>
        public static readonly IEnumerable<string> LowerModernLatinAlphabet = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" };

        /// <summary>
        /// The (upper-case) modern Latin alphabet
        /// </summary>
        public static readonly IEnumerable<string> UpperModernLatinAlphabet = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
    }
}
