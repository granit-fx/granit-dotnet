using System.Text.Json;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Every JSON file under <c>src/**/Localization/**/*.json</c> MUST follow the framework
/// envelope <c>{ "culture": "&lt;code&gt;", "texts": { ... } }</c>. Files missing the
/// envelope crash the host at startup with a runtime
/// <c>InvalidOperationException</c> from <c>JsonLocalizationDictionaryBuilder</c> the
/// first time someone hits a culture that triggers the lazy resource load — typically
/// in production, far from where the bad commit was authored.
/// </summary>
/// <remarks>
/// <para>
/// Reproduces the actual failure mode observed when <c>Granit.Entities.Views.Endpoints</c>
/// shipped with flat-shape locale files (PR #1638): the framework swallowed the
/// shape mismatch silently in CI and only surfaced when a Czech (cs) request was
/// served. This test catches the flat shape at build time across every culture file
/// in the repository in a single fact, so the next missing-envelope file fails CI
/// before merge.
/// </para>
/// <para>
/// The check also asserts the <c>culture</c> property matches the file basename
/// (e.g. <c>cs.json</c> → <c>"cs"</c>) so a copy-paste rename like <c>en.json → de.json</c>
/// without updating the inner <c>"culture"</c> field is also caught.
/// </para>
/// </remarks>
public sealed class LocalizationFileShapeTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_localization_json_file_should_use_the_culture_envelope()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        IReadOnlyList<string> files = EnumerateLocalizationFiles(srcDir).ToList();

        files.ShouldNotBeEmpty(
            "No localization JSON files discovered — the test cannot run. " +
            "Expected at least one file under src/**/Localization/**/.");

        List<string> violations = [];

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(RepoRoot, file);
            string expectedCulture = Path.GetFileNameWithoutExtension(file);

            JsonDocument document;
            try
            {
                using FileStream stream = File.OpenRead(file);
                document = JsonDocument.Parse(stream);
            }
            catch (JsonException ex)
            {
                violations.Add($"{relativePath}: invalid JSON ({ex.Message})");
                continue;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    violations.Add($"{relativePath}: root must be a JSON object, was {root.ValueKind}");
                    continue;
                }

                if (!root.TryGetProperty("culture", out JsonElement culture)
                    || culture.ValueKind != JsonValueKind.String)
                {
                    violations.Add(
                        $"{relativePath}: missing top-level 'culture' string property — " +
                        $"wrap content in {{ \"culture\": \"{expectedCulture}\", \"texts\": {{ ... }} }}");
                    continue;
                }

                if (!root.TryGetProperty("texts", out JsonElement texts)
                    || texts.ValueKind != JsonValueKind.Object)
                {
                    violations.Add(
                        $"{relativePath}: missing top-level 'texts' object property — " +
                        $"wrap content in {{ \"culture\": \"{expectedCulture}\", \"texts\": {{ ... }} }}");
                    continue;
                }

                string declaredCulture = culture.GetString()!;
                if (!string.Equals(declaredCulture, expectedCulture, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{relativePath}: 'culture' is \"{declaredCulture}\" but file is named " +
                        $"\"{expectedCulture}.json\" — they must match");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"{violations.Count} localization JSON file(s) violate the framework envelope. " +
            "Every src/**/Localization/**/*.json must be of the form " +
            "{ \"culture\": \"<code>\", \"texts\": { ... } }:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal)));
    }

    private static IEnumerable<string> EnumerateLocalizationFiles(string srcDir)
    {
        foreach (string file in Directory.EnumerateFiles(srcDir, "*.json", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            string normalized = file.Replace(Path.DirectorySeparatorChar, '/');
            int marker = normalized.IndexOf("/Localization/", StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            yield return file;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(LocalizationFileShapeTests).Assembly.Location);
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
