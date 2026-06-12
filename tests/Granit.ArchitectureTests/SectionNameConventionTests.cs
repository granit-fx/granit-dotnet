using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces the Granit <c>SectionName</c> convention for options classes:
/// <list type="bullet">
///   <item>Hierarchical, ASP.NET-style, colon-separated (<c>"Foo:Bar:Baz"</c>).</item>
///   <item>No PascalCase-glued tokens (<c>"FooBar"</c> forbidden when <c>"Foo:Bar"</c> applies).</item>
///   <item><c>"Granit"</c> MUST NOT be used as a root segment — it is an internal namespace
///         prefix, not a configuration root.</item>
///   <item>No two distinct <c>Options</c> classes may share the same section path
///         (parent/child collisions silently break binding).</item>
/// </list>
/// </summary>
public sealed partial class SectionNameConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// A handful of single-segment top-level sections kept on purpose because their owning
    /// project IS the root namespace (no sub-feature). They satisfy the convention
    /// "namespace without Granit prefix" — they just happen to be one segment long.
    /// </summary>
    private static readonly HashSet<string> AllowedSingleSegmentSections =
    [
        "AI",
        "Auditing",
        "Authentication",
        "Authorization",
        "BackgroundJobs",
        "Bff",
        "BlobStorage",
        "Browsing",
        "Cache",
        "Encryption",
        "Indexing",
        "IpGeolocation",
        "Mcp",
        "EntityMerge",
        "Hostnames",
        "MultiTenancy",
        "Notifications",
        "Observability",
        "OpenIddict",
        "Presence",
        "Privacy",
        "QueryEngine",
        "RateLimiting",
        "Settings",
        "TextExtraction",
        "Vault",
        "Webhooks",
        "Wolverine",
    ];

    /// <summary>
    /// SectionName values must never start with <c>"Granit"</c> — Granit is the internal
    /// namespace prefix, not a configuration root.
    /// </summary>
    [Fact]
    public void SectionName_must_not_start_with_Granit()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach ((string file, int line, string value) in EnumerateSectionNames(srcDir))
        {
            if (value.StartsWith("Granit:", StringComparison.Ordinal)
                || value.Equals("Granit", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, file)}:{line} \"{value}\"");
            }
        }

        violations.ShouldBeEmpty(
            "SectionName must not be prefixed with 'Granit:' — Granit is a code-level "
            + "namespace, not a configuration root. Use the bare hierarchical path "
            + "(e.g. 'IO:TempFiles', not 'Granit:IO:TempFiles'). Violators: "
            + string.Join("; ", violations));
    }

    /// <summary>
    /// SectionName values must be hierarchical (colon-separated). Multi-word names
    /// that span two clear concepts (<c>"FooBar"</c>) must be split with <c>:</c>.
    /// Single-segment sections are allowed only for top-level modules (see
    /// <see cref="AllowedSingleSegmentSections"/>).
    /// </summary>
    [Fact]
    public void SectionName_must_be_hierarchical()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach ((string file, int line, string value) in EnumerateSectionNames(srcDir))
        {
            if (value.Contains(':', StringComparison.Ordinal))
            {
                continue;
            }

            if (AllowedSingleSegmentSections.Contains(value))
            {
                continue;
            }

            violations.Add($"{Path.GetRelativePath(RepoRoot, file)}:{line} \"{value}\"");
        }

        violations.ShouldBeEmpty(
            "Flat (non-colon-separated) SectionName values are reserved for top-level modules. "
            + "Compound names must use the hierarchical 'Parent:Child' form. "
            + "If you intentionally introduce a new top-level section, add it to "
            + nameof(AllowedSingleSegmentSections) + ". Violators: "
            + string.Join("; ", violations));
    }

    /// <summary>
    /// No two <c>Options</c> classes may declare the same SectionName. A section path
    /// can be bound to its own Options class AND host child sub-sections (ASP.NET
    /// configuration supports that) — what is forbidden is two unrelated Options
    /// classes pointing at the same string.
    /// </summary>
    [Fact]
    public void SectionName_values_must_be_unique()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        Dictionary<string, List<string>> byValue = [];

        foreach ((string file, int line, string value) in EnumerateSectionNames(srcDir))
        {
            if (!byValue.TryGetValue(value, out List<string>? locations))
            {
                locations = [];
                byValue[value] = locations;
            }

            locations.Add($"{Path.GetRelativePath(RepoRoot, file)}:{line}");
        }

        List<string> duplicates = [.. byValue
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => $"\"{kv.Key}\" used by [{string.Join(", ", kv.Value)}]")];

        duplicates.ShouldBeEmpty(
            "SectionName values must be unique across the framework. Duplicates: "
            + string.Join("; ", duplicates));
    }

    private static IEnumerable<(string File, int Line, string Value)> EnumerateSectionNames(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            int lineNumber = 0;
            foreach (string line in File.ReadLines(csFile))
            {
                lineNumber++;
                Match match = SectionNameConstant().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                yield return (csFile, lineNumber, match.Groups[1].Value);
            }
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(SectionNameConventionTests).Assembly.Location);
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

    /// <summary>
    /// Matches <c>public const string SectionName = "Value";</c>.
    /// Group 1: the string value.
    /// </summary>
    [GeneratedRegex(@"public\s+const\s+string\s+SectionName\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex SectionNameConstant();
}
