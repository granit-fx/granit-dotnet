namespace Granit.LanguageDetection.Trigram.Internal;

/// <summary>
/// Rank-distance scorer between an input trigram rank table and every language
/// profile within a single script. Mirrors the Franc scoring path: out-of-profile
/// trigrams contribute a fixed constant cost (<see cref="MaxDifference"/>), giving
/// distances bounded by <c>input_size × 300</c> regardless of profile shape.
/// </summary>
internal static class RankedDistanceScorer
{
    /// <summary>Fixed cost for an input trigram not present in the language profile.</summary>
    public const int MaxDifference = 300;

    /// <summary>
    /// Returns the ISO 639-3 code of the closest language in <paramref name="profiles"/>,
    /// or <c>null</c> when <paramref name="inputRanks"/> is empty.
    /// </summary>
    public static string? Detect(Dictionary<string, int> inputRanks, IReadOnlyList<LanguageProfile> profiles)
    {
        if (inputRanks.Count == 0 || profiles.Count == 0)
        {
            return null;
        }

        LanguageProfile? best = null;
        long bestDistance = long.MaxValue;

        foreach (LanguageProfile profile in profiles)
        {
            long distance = ComputeDistance(inputRanks, profile);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = profile;
            }
        }

        return best?.Iso639_3Code;
    }

    private static long ComputeDistance(Dictionary<string, int> inputRanks, LanguageProfile profile)
    {
        long distance = 0;
        foreach ((string trigram, int inputRank) in inputRanks)
        {
            if (profile.TrigramRanks.TryGetValue(trigram, out int profileRank))
            {
                distance += Math.Abs(inputRank - profileRank);
            }
            else
            {
                distance += MaxDifference;
            }
        }

        return distance;
    }
}
