using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace Granit.LanguageDetection.Trigram.Internal;

/// <summary>
/// Loads <see cref="LanguageProfileBundle"/> from the embedded Franc JSON.
/// </summary>
/// <remarks>
/// JSON shape: <c>{ "Latin": { "eng": "the|of |and|...", "fra": "..." }, "Cyrillic": { ... } }</c>.
/// Trigrams within a language are pipe-separated and ordered by descending
/// frequency; rank is implicit in position (first trigram = rank 0).
/// </remarks>
internal static class LanguageProfileLoader
{
    private const string EmbeddedResourceName = "Granit.LanguageDetection.Trigram.Resources.profiles.json";

    public static LanguageProfileBundle Load()
    {
        using Stream stream = OpenEmbeddedResource(EmbeddedResourceName);
        using var doc = JsonDocument.Parse(stream);

        Dictionary<string, IReadOnlyList<LanguageProfile>> byScript = new(StringComparer.Ordinal);

        foreach (JsonProperty scriptProp in doc.RootElement.EnumerateObject())
        {
            List<LanguageProfile> profiles = [];
            foreach (JsonProperty langProp in scriptProp.Value.EnumerateObject())
            {
                string iso3 = langProp.Name;
                string trigramString = langProp.Value.GetString() ?? string.Empty;
                FrozenDictionary<string, int> ranks = ParseTrigrams(trigramString);
                profiles.Add(new LanguageProfile(iso3, ranks));
            }

            byScript[scriptProp.Name] = profiles;
        }

        return new LanguageProfileBundle(byScript);
    }

    private static FrozenDictionary<string, int> ParseTrigrams(string trigramString)
    {
        Dictionary<string, int> ranks = new(StringComparer.Ordinal);
        int rank = 0;
        int start = 0;
        for (int i = 0; i <= trigramString.Length; i++)
        {
            if (i == trigramString.Length || trigramString[i] == '|')
            {
                int len = i - start;
                if (len > 0)
                {
                    string trigram = trigramString.Substring(start, len);
                    ranks.TryAdd(trigram, rank++);
                }
                start = i + 1;
            }
        }

        return ranks.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static Stream OpenEmbeddedResource(string name)
    {
        Assembly assembly = typeof(LanguageProfileLoader).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{name}' not found. Manifest contains: " +
                string.Join(", ", assembly.GetManifestResourceNames()));
        }

        return stream;
    }
}
