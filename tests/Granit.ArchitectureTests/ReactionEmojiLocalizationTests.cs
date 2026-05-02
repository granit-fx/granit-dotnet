using System.Text.Json;
using Granit.Timeline.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces story C3 acceptance — every emoji in
/// <see cref="ReactionEmojiCatalog.All"/> MUST have a <c>Reaction:{key}</c>
/// resource key in all 15 base cultures of
/// <c>Granit.Timeline.Endpoints/Localization/</c>. Regional variants
/// (<c>fr-CA</c>, <c>en-GB</c>, <c>pt-BR</c>) are exempt — they ship only
/// differing keys and fall through to their parent culture.
/// </summary>
public sealed class ReactionEmojiLocalizationTests
{
    private static readonly string[] BaseCultures =
    [
        "en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja",
        "pl", "tr", "ko", "sv", "cs", "hi",
    ];

    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_emoji_should_have_localization_keys_in_all_base_cultures()
    {
        ReactionEmojiCatalog.All.ShouldNotBeEmpty();

        string localizationDir = Path.Join(RepoRoot, "src", "Granit.Timeline.Endpoints", "Localization");
        Directory.Exists(localizationDir).ShouldBeTrue(
            $"Granit.Timeline.Endpoints/Localization/ not found at {localizationDir}");

        List<string> missing = [];
        foreach (string emoji in ReactionEmojiCatalog.All)
        {
            string key = $"Reaction:{emoji}";
            foreach (string culture in BaseCultures)
            {
                if (!LocateKeyInCultureFiles(localizationDir, culture, key))
                {
                    missing.Add($"{emoji} -> {culture} (expected key '{key}' in src/Granit.Timeline.Endpoints/Localization/**/{culture}.json)");
                }
            }
        }

        missing.ShouldBeEmpty(
            "Story C3: every emoji in ReactionEmojiCatalog.All must have a 'Reaction:{key}' key in all 15 base cultures. "
            + $"{missing.Count} key(s) missing:" + Environment.NewLine
            + string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }

    private static bool LocateKeyInCultureFiles(string localizationDir, string culture, string resourceKey)
    {
        string[] files = Directory.GetFiles(localizationDir, $"{culture}.json", SearchOption.AllDirectories);
        return files.Any(file => FileContainsKey(file, resourceKey));
    }

    private static bool FileContainsKey(string filePath, string key)
    {
        using FileStream stream = File.OpenRead(filePath);
        using var doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (root.TryGetProperty("texts", out JsonElement texts)
            && texts.ValueKind == JsonValueKind.Object
            && texts.TryGetProperty(key, out _))
        {
            return true;
        }
        return root.TryGetProperty(key, out _);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Join(dir.FullName, "Granit.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (Granit.slnx not found).");
    }
}
