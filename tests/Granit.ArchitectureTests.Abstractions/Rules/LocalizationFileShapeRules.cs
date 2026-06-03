using System.Text.Json;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rule: every localization JSON file must follow the framework envelope
/// <c>{ "culture": "&lt;code&gt;", "texts": { ... } }</c>.
/// </summary>
public static class LocalizationFileShapeRules
{
    /// <summary>
    /// Asserts that every <c>*.json</c> file under a <c>Localization/</c> folder within
    /// <paramref name="srcDir"/> uses the framework culture envelope and that the declared
    /// <c>"culture"</c> value matches the file's base name.
    /// </summary>
    /// <param name="srcDir">The <c>src/</c> directory to scan recursively for localization files.</param>
    /// <param name="repoRoot">Repository root, used to render violation paths relative to the repo.</param>
    /// <param name="allowEmpty">
    /// When <c>false</c> (default), the absence of any localization file fails the test — a canary
    /// that catches a broken glob or relocated files in repos that <i>do</i> ship localization.
    /// Pass <c>true</c> for minimalist consumers (e.g. an IoT/worker service) that legitimately
    /// ship no localized strings; per-file envelope validation still runs for any file that appears.
    /// </param>
    public static void EveryLocalizationFileShouldUseCultureEnvelope(string srcDir, string repoRoot, bool allowEmpty = false)
    {
        IReadOnlyList<string> files = [.. EnumerateLocalizationFiles(srcDir)];

        if (files.Count == 0)
        {
            if (allowEmpty)
            {
                return;
            }

            files.ShouldNotBeEmpty(
                "No localization JSON files discovered — the test cannot run. " +
                "Expected at least one file under src/**/Localization/**/. " +
                "Pass allowEmpty: true for minimalist consumers that legitimately ship none.");
        }

        List<string> violations = [];

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(repoRoot, file);
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
                        $"{relativePath}: missing 'culture' string — wrap content in " +
                        $"{{ \"culture\": \"{expectedCulture}\", \"texts\": {{ ... }} }}");
                    continue;
                }

                if (!root.TryGetProperty("texts", out JsonElement texts)
                    || texts.ValueKind != JsonValueKind.Object)
                {
                    violations.Add(
                        $"{relativePath}: missing 'texts' object — wrap content in " +
                        $"{{ \"culture\": \"{expectedCulture}\", \"texts\": {{ ... }} }}");
                    continue;
                }

                string declaredCulture = culture.GetString()!;
                if (!string.Equals(declaredCulture, expectedCulture, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{relativePath}: 'culture' is \"{declaredCulture}\" but file is " +
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
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            if (file.Replace(Path.DirectorySeparatorChar, '/').Contains("/Localization/", StringComparison.Ordinal))
            {
                yield return file;
            }
        }
    }
}
