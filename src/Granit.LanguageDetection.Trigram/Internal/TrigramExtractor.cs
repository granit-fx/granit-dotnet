using System.Text;

namespace Granit.LanguageDetection.Trigram.Internal;

/// <summary>
/// Extracts character trigrams from a sample, applying the Franc-equivalent
/// pre-processing: lowercase, replace every non-letter run with a single space,
/// pad with a leading and trailing space, then slide a 3-char window. The result
/// is a frequency table; the scorer turns it into a rank table.
/// </summary>
internal static class TrigramExtractor
{
    /// <summary>
    /// Returns the trigram → rank table (0-based, 0 = most frequent) for the top
    /// <paramref name="topN"/> trigrams of <paramref name="sample"/>.
    /// </summary>
    public static Dictionary<string, int> ExtractRanks(ReadOnlySpan<char> sample, int topN)
    {
        Dictionary<string, int> counts = CountTrigrams(sample);

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topN)
            .Select((kv, idx) => (kv.Key, Rank: idx))
            .ToDictionary(t => t.Key, t => t.Rank, StringComparer.Ordinal);
    }

    private static Dictionary<string, int> CountTrigrams(ReadOnlySpan<char> sample)
    {
        // Build the normalised sliding string: lowercase + non-letter → space + collapse
        // consecutive spaces. Prepend/append a single space to give the first and last
        // letter a word-boundary cue.
        StringBuilder sb = new(sample.Length + 2);
        sb.Append(' ');
        bool lastWasSpace = true;

        foreach (char c in sample)
        {
            if (char.IsLetter(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        if (!lastWasSpace)
        {
            sb.Append(' ');
        }

        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        if (sb.Length < 3)
        {
            return counts;
        }

        // Pre-materialise the normalised text so the inner loop avoids per-iteration
        // StringBuilder access. The allocation is bounded by sample.Length + 2.
        string normalised = sb.ToString();
        for (int i = 0; i <= normalised.Length - 3; i++)
        {
            string trigram = normalised.Substring(i, 3);
            counts.TryGetValue(trigram, out int existing);
            counts[trigram] = existing + 1;
        }

        return counts;
    }
}
