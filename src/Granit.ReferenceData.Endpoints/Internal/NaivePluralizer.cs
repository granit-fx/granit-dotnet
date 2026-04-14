namespace Granit.ReferenceData.Endpoints.Internal;

/// <summary>
/// Minimal English pluralizer for converting PascalCase entity names to plural forms.
/// Covers common patterns without requiring a Humanizer dependency.
/// Use <see cref="Options.ReferenceDataEndpointsOptions.EntitySegment"/> to override edge cases.
/// </summary>
internal static class NaivePluralizer
{
    private static readonly HashSet<char> Vowels = ['a', 'e', 'i', 'o', 'u'];

    /// <summary>
    /// Returns a naive English plural of <paramref name="singular"/>.
    /// Must be called on PascalCase names <b>before</b> kebab-case conversion.
    /// </summary>
    internal static string Pluralize(string singular)
    {
        if (string.IsNullOrEmpty(singular))
        {
            return singular;
        }

        // Consonant + y → ies (Country → Countries, Category → Categories)
        // Vowel + y → just +s (Survey → Surveys, Key → Keys)
        if (singular.EndsWith('y') && singular.Length >= 2 && !Vowels.Contains(char.ToLowerInvariant(singular[^2])))
        {
            return string.Concat(singular.AsSpan(0, singular.Length - 1), "ies");
        }

        // Sibilant endings → +es (Status → Statuses, Tax → Taxes, Address → Addresses)
        if (singular.EndsWith('s') || singular.EndsWith('x') || singular.EndsWith('z')
            || singular.EndsWith("sh", StringComparison.Ordinal)
            || singular.EndsWith("ch", StringComparison.Ordinal))
        {
            return singular + "es";
        }

        // Default → +s
        return singular + "s";
    }
}
