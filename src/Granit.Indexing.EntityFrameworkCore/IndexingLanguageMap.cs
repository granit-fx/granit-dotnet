namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Stable mapping from ISO 639-1 language codes to Postgres text-search dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// The mapping covers every dictionary shipped with a vanilla Postgres install (no
/// extensions required). Codes outside the list fall back to the configured default
/// dictionary at query time.
/// </para>
/// <para>
/// Codes are matched case-insensitively against the ISO 639-1 two-letter code; both
/// <c>en</c> and <c>EN</c> map to <c>english</c>.
/// </para>
/// </remarks>
public static class IndexingLanguageMap
{
    private static readonly Dictionary<string, string> Mappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "english",
        ["fr"] = "french",
        ["es"] = "spanish",
        ["de"] = "german",
        ["it"] = "italian",
        ["nl"] = "dutch",
        ["pt"] = "portuguese",
        ["ru"] = "russian",
        ["sv"] = "swedish",
        ["no"] = "norwegian",
        ["da"] = "danish",
        ["fi"] = "finnish",
        ["tr"] = "turkish",
        ["hu"] = "hungarian",
        ["ro"] = "romanian",
        ["id"] = "indonesian",
        ["el"] = "greek",
        ["lt"] = "lithuanian",
        ["ar"] = "arabic",
        ["ga"] = "irish",
        ["hi"] = "hindi",
        ["ne"] = "nepali",
        ["sr"] = "serbian",
        ["ta"] = "tamil",
        ["yi"] = "yiddish",
    };

    /// <summary>
    /// Returns the Postgres dictionary name for <paramref name="iso639Code"/>, or
    /// <paramref name="fallback"/> when no mapping exists.
    /// </summary>
    public static string GetPostgresDictionary(string? iso639Code, string fallback)
    {
        ArgumentException.ThrowIfNullOrEmpty(fallback);
        if (string.IsNullOrEmpty(iso639Code))
        {
            return fallback;
        }

        return Mappings.TryGetValue(iso639Code, out string? dict) ? dict : fallback;
    }

    /// <summary>
    /// Returns <c>true</c> when an ISO 639-1 code has a known Postgres dictionary.
    /// </summary>
    public static bool HasMapping(string iso639Code)
    {
        ArgumentException.ThrowIfNullOrEmpty(iso639Code);
        return Mappings.ContainsKey(iso639Code);
    }
}
