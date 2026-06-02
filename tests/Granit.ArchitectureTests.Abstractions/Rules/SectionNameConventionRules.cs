using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rules for the Granit <c>SectionName</c> configuration-key convention.
/// </summary>
public static partial class SectionNameConventionRules
{
    /// <summary>
    /// SectionName values must never start with <c>"Granit"</c> — Granit is the internal
    /// namespace prefix, not a configuration root.
    /// </summary>
    public static void SectionNameMustNotStartWithGranit(string srcDir, string repoRoot)
    {
        List<string> violations = [];

        foreach ((string file, int line, string value) in EnumerateSectionNames(srcDir))
        {
            if (value.StartsWith("Granit:", StringComparison.Ordinal)
                || string.Equals(value, "Granit", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(repoRoot, file)}:{line} \"{value}\"");
            }
        }

        violations.ShouldBeEmpty(
            "SectionName must not be prefixed with 'Granit:' — Granit is a code-level namespace, " +
            "not a configuration root. Use the bare hierarchical path (e.g. 'IO:TempFiles'). " +
            "Violators: " + string.Join("; ", violations));
    }

    /// <summary>
    /// SectionName values must be colon-separated. Single-segment values are only allowed
    /// for top-level module sections listed in <paramref name="allowedSingleSegments"/>.
    /// </summary>
    public static void SectionNameMustBeHierarchical(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string> allowedSingleSegments)
    {
        List<string> violations = [];

        foreach ((string file, int line, string value) in EnumerateSectionNames(srcDir))
        {
            if (value.Contains(':', StringComparison.Ordinal))
            {
                continue;
            }

            if (allowedSingleSegments.Contains(value))
            {
                continue;
            }

            violations.Add($"{Path.GetRelativePath(repoRoot, file)}:{line} \"{value}\"");
        }

        violations.ShouldBeEmpty(
            "Flat (non-colon-separated) SectionName values are reserved for top-level modules. " +
            "Compound names must use the hierarchical 'Parent:Child' form. " +
            "If you intentionally introduce a new top-level section, add it to the allowed set. " +
            "Violators: " + string.Join("; ", violations));
    }

    /// <summary>
    /// No two Options classes may declare the same SectionName path.
    /// </summary>
    public static void SectionNameValuesMustBeUnique(string srcDir, string repoRoot)
    {
        Dictionary<string, List<string>> byValue = [];

        foreach ((string file, int line, string value) in EnumerateSectionNames(srcDir))
        {
            if (!byValue.TryGetValue(value, out List<string>? locations))
            {
                locations = [];
                byValue[value] = locations;
            }

            locations.Add($"{Path.GetRelativePath(repoRoot, file)}:{line}");
        }

        List<string> duplicates = [.. byValue
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => $"\"{kv.Key}\" used by [{string.Join(", ", kv.Value)}]")];

        duplicates.ShouldBeEmpty(
            "SectionName values must be unique across the repo. Duplicates: " +
            string.Join("; ", duplicates));
    }

    private static IEnumerable<(string File, int Line, string Value)> EnumerateSectionNames(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(csFile))
            {
                continue;
            }

            int lineNumber = 0;
            foreach (string line in File.ReadLines(csFile))
            {
                lineNumber++;
                Match match = SectionNameConstant().Match(line);
                if (match.Success)
                {
                    yield return (csFile, lineNumber, match.Groups[1].Value);
                }
            }
        }
    }

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    [GeneratedRegex(@"public\s+const\s+string\s+SectionName\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex SectionNameConstant();
}
