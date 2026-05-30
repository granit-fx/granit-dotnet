namespace Granit.AI.Sampling;

/// <summary>
/// Helpers for sampling free-text content before an LLM call.
/// </summary>
public static class AIContentSampler
{
    /// <summary>
    /// Returns at most <paramref name="maxChars"/> characters from the head of
    /// <paramref name="content"/>, never splitting a UTF-16 surrogate pair.
    /// </summary>
    /// <remarks>
    /// A naive <c>content[..maxChars]</c> slice can land between the high and low
    /// surrogate of a non-BMP code point (emoji, many CJK ideographs, OCR'd glyphs),
    /// producing a lone surrogate. Downstream the transport serializer either rejects
    /// it (surfacing as a spurious transport failure) or substitutes <c>U+FFFD</c>
    /// (silently corrupting the sample). When the cut would split a pair, this helper
    /// backs off by one code unit so the returned span is always valid UTF-16.
    /// </remarks>
    /// <param name="content">The source text. Must not be <c>null</c>.</param>
    /// <param name="maxChars">
    /// Maximum number of UTF-16 code units to keep. Must be non-negative.
    /// </param>
    public static string TruncateOnCodePoint(string content, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(maxChars);

        if (content.Length <= maxChars)
        {
            return content;
        }

        int cut = maxChars;
        if (cut > 0 && char.IsHighSurrogate(content[cut - 1]))
        {
            cut--;
        }

        return content[..cut];
    }
}
