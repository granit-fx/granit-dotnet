using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Granit.Privacy.DataExport;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Companion to <see cref="PermissionLocalizationCompletenessTests"/> for privacy export
/// scopes: every <see cref="IPrivacyDataProvider.DisplayKey"/> shipped by the framework
/// MUST resolve in the <c>PrivacyEndpoints</c> localization resource across all 15 base
/// cultures. The scope selector UI receives the raw key over the wire
/// (<c>PrivacyExportScopeResponse.DisplayKey</c>) and resolves it against the merged
/// localization map — a missing key surfaces the technical identifier to the data
/// subject in the GDPR export UI (#2977).
/// </summary>
/// <remarks>
/// The owning resource is <c>Granit.Privacy.Endpoints</c> (not the provider satellites):
/// scope labels only matter where a UI exists, and that package already hosts the
/// privacy-facing <c>Permission:</c> and <c>Workspace.</c> labels. Regional variants
/// (<c>fr-CA</c>, <c>en-GB</c>, <c>pt-BR</c>) are exempt — they ship only differing keys
/// and fall through to their parent culture.
/// </remarks>
public sealed partial class PrivacyScopeLocalizationCompletenessTests
{
    private static readonly string[] BaseCultures =
    [
        "en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja",
        "pl", "tr", "ko", "sv", "cs", "hi",
    ];

    private const string OwningResourceDir = "src/Granit.Privacy.Endpoints/Localization/PrivacyEndpoints";

    private static readonly string RepoRoot = FindRepoRoot();

    [GeneratedRegex(@"^Privacy\.Scopes\.[A-Z][A-Za-z0-9]*$")]
    private static partial Regex DisplayKeyFormat();

    [Fact]
    public void Every_provider_DisplayKey_should_resolve_in_all_base_cultures_of_the_PrivacyEndpoints_resource()
    {
        List<(string Assembly, string DisplayKey)> displayKeys = DiscoverDisplayKeys();
        displayKeys.ShouldNotBeEmpty(
            "No IPrivacyDataProvider implementations discovered — the test cannot run.");

        Dictionary<string, HashSet<string>> textsByCulture = LoadOwningResource();

        List<string> missing = [];
        foreach ((string assembly, string displayKey) in displayKeys)
        {
            foreach (string culture in BaseCultures)
            {
                if (!textsByCulture[culture].Contains(displayKey))
                {
                    missing.Add($"{displayKey} (from {assembly}) -> {culture}");
                }
            }
        }

        missing.ShouldBeEmpty(
            $"Every IPrivacyDataProvider.DisplayKey must have a key in all {BaseCultures.Length} base cultures " +
            $"of {OwningResourceDir}/. {missing.Count} key(s) missing:" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Every_provider_DisplayKey_should_follow_the_Privacy_Scopes_format()
    {
        List<string> violations = [.. DiscoverDisplayKeys()
            .Where(entry => !DisplayKeyFormat().IsMatch(entry.DisplayKey))
            .Select(entry => $"{entry.Assembly}: \"{entry.DisplayKey}\"")];

        violations.ShouldBeEmpty(
            "Every IPrivacyDataProvider.DisplayKey must match 'Privacy.Scopes.{PascalCase}':" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static List<(string Assembly, string DisplayKey)> DiscoverDisplayKeys()
    {
        string outputDir = Path.GetDirectoryName(typeof(PrivacyScopeLocalizationCompletenessTests).Assembly.Location)!;

        List<(string, string)> result = [];
        foreach (string path in Directory.GetFiles(outputDir, "Granit.*.dll"))
        {
            if (Path.GetFileNameWithoutExtension(path).Contains("Tests", StringComparison.Ordinal))
            {
                continue;
            }

            Assembly assembly;
            Type[] types;
            try
            {
                assembly = Assembly.LoadFrom(path);
                types = assembly.GetTypes();
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (Type provider in types.Where(t =>
                !t.IsAbstract && !t.IsInterface && typeof(IPrivacyDataProvider).IsAssignableFrom(t)))
            {
                PropertyInfo property = provider.GetProperty(
                    nameof(IPrivacyDataProvider.DisplayKey), BindingFlags.Public | BindingFlags.Static)
                    ?? throw new InvalidOperationException(
                        $"{provider.FullName} does not expose the static DisplayKey property.");

                result.Add((assembly.GetName().Name!, (string)property.GetValue(null)!));
            }
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>> LoadOwningResource()
    {
        Dictionary<string, HashSet<string>> textsByCulture = [];
        foreach (string culture in BaseCultures)
        {
            string file = Path.Join(RepoRoot, OwningResourceDir, $"{culture}.json");
            File.Exists(file).ShouldBeTrue($"Expected localization file not found: {file}");

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            textsByCulture[culture] = [.. document.RootElement
                .GetProperty("texts")
                .EnumerateObject()
                .Select(p => p.Name)];
        }

        return textsByCulture;
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(PrivacyScopeLocalizationCompletenessTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }
}
