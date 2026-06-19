using System.Reflection;
using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable filesystem-based rules for the OpenAPI tag-name convention from CLAUDE.md.
/// Consume via <c>Granit.ArchitectureTests.Abstractions</c> — call from any repo's
/// architecture test project by passing the path to its own <c>src/</c> directory.
/// </summary>
/// <remarks>
/// <para>
/// <b>Typical usage</b> (mirrors the Showcase pattern):
/// <code>
/// private static readonly string SrcDir = Path.Join(
///     OpenApiTagConventionRules.FindRepoRoot(typeof(MyArchTests).Assembly), "src");
///
/// [Fact]
/// public void No_hardcoded_TagName() =>
///     OpenApiTagConventionRules.NoEndpointsFileShouldDeclareHardcodedTagNameConstant(SrcDir);
///
/// [Fact]
/// public void TagName_defaults_follow_convention() =>
///     OpenApiTagConventionRules.AllTagNameDefaultsShouldFollowConvention(SrcDir);
/// </code>
/// </para>
/// </remarks>
public static partial class OpenApiTagConventionRules
{
    /// <summary>
    /// Asserts that no <c>*Endpoints.cs</c> file under <paramref name="srcDir"/> declares a
    /// <c>private const string TagName</c>. All tag names must come from <c>*EndpointsOptions</c>
    /// so that (a) <see cref="AllTagNameDefaultsShouldFollowConvention"/> covers them, and
    /// (b) host apps can override them without recompilation.
    /// </summary>
    public static void NoEndpointsFileShouldDeclareHardcodedTagNameConstant(string srcDir)
    {
        List<string> violations = [];

        foreach (string file in Directory.GetFiles(srcDir, "*Endpoints.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(file))
            {
                continue;
            }

            foreach (string line in File.ReadLines(file))
            {
                if (HardcodedTagNameConstant().IsMatch(line))
                {
                    violations.Add(Path.GetRelativePath(srcDir, file));
                    break;
                }
            }
        }

        violations.ShouldBeEmpty(
            "Endpoint implementation files must not declare 'private const string TagName'. " +
            "Source the tag from the module's *EndpointsOptions.TagName so hosts can override it " +
            "and the convention test covers the value. " +
            $"{violations.Count} violation(s):" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Asserts that every <c>TagName</c> default declared in a <c>*EndpointsOptions.cs</c> file
    /// under <paramref name="srcDir"/> follows either <c>Title Case With Spaces</c> (single-tag
    /// modules) or the <c>&lt;Module&gt; - &lt;SubGroup&gt;</c> compound format (space-dash-space,
    /// both sides Title Case).
    /// </summary>
    public static void AllTagNameDefaultsShouldFollowConvention(string srcDir)
    {
        List<TagNameDefault> defaults = ScanTagNameDefaults(srcDir);
        defaults.ShouldNotBeEmpty(
            "No TagName defaults discovered — the test cannot run. Ensure the *.Endpoints " +
            "modules under src/ ship their EndpointsOptions classes with public string TagName defaults.");

        List<string> violations = [];

        foreach (TagNameDefault entry in defaults)
        {
            if (!IsValidTagName(entry.Value))
            {
                string relative = Path.GetRelativePath(srcDir, entry.FilePath);
                violations.Add($"{relative}: {entry.PropertyName} = \"{entry.Value}\"");
            }
        }

        violations.ShouldBeEmpty(
            "OpenAPI TagName defaults must follow CLAUDE.md tagging convention — " +
            "either 'Title Case With Spaces' (single-tag modules) " +
            "or '<Module> - <SubGroup>' (multi-tag modules, space-dash-space, both sides Title Case). " +
            $"{violations.Count} violation(s):" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="tag"/> follows the OpenAPI tag-name convention:
    /// either <c>Title Case With Spaces</c> (single segment, no internal hyphen) or
    /// <c>&lt;Module&gt; - &lt;SubGroup&gt;</c> (two Title Case segments separated by space-dash-space).
    /// </summary>
    public static bool IsValidTagName(string tag)
    {
        if (string.IsNullOrEmpty(tag) || tag != tag.Trim())
        {
            return false;
        }

        string[] segments = SplitOnSpaceDashSpace(tag);
        return segments.Length switch
        {
            1 => IsTitleCasePhrase(segments[0]) && !segments[0].Contains('-', StringComparison.Ordinal),
            2 => segments.All(IsTitleCasePhrase),
            _ => false,
        };
    }

    /// <summary>
    /// Walks up from the location of <paramref name="callerAssembly"/> until a <c>.git</c> entry
    /// is found and returns that directory as the repository root.
    /// </summary>
    /// <exception cref="InvalidOperationException">When no <c>.git</c> directory is found.</exception>
    public static string FindRepoRoot(Assembly callerAssembly)
    {
        string? dir = Path.GetDirectoryName(callerAssembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Join(dir, ".git")) || File.Exists(Path.Join(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            $"Could not find repository root (.git directory) starting from '{callerAssembly.Location}'.");
    }

    /// <summary>
    /// Asserts that every <c>*.Endpoints</c> package directory directly under
    /// <paramref name="srcDir"/> contains at least one <c>*EndpointsOptions.cs</c> file.
    /// Without an options class the <see cref="AllTagNameDefaultsShouldFollowConvention"/> rule
    /// cannot observe the package's <c>TagName</c> defaults, and host applications have no
    /// override surface.
    /// Packages whose tag names are computed dynamically at runtime (instead of being declared as
    /// a static options default) may be exempted by passing their folder name in
    /// <paramref name="exemptPackageNames"/>.
    /// </summary>
    public static void EveryEndpointsPackageShouldHaveEndpointsOptions(
        string srcDir,
        params string[] exemptPackageNames)
    {
        HashSet<string> exemptions = new(exemptPackageNames, StringComparer.OrdinalIgnoreCase);
        List<string> violations = [];

        foreach (string packageDir in Directory.GetDirectories(srcDir, "*.Endpoints", SearchOption.TopDirectoryOnly))
        {
            string packageName = Path.GetFileName(packageDir);
            if (exemptions.Contains(packageName))
            {
                continue;
            }

            bool hasOptions = Directory.GetFiles(packageDir, "*EndpointsOptions.cs", SearchOption.AllDirectories)
                .Any(f => !IsInBuildOutput(f));

            if (!hasOptions)
            {
                violations.Add(packageName);
            }
        }

        violations.ShouldBeEmpty(
            "Every *.Endpoints package must ship at least one *EndpointsOptions class so that its " +
            "TagName defaults are observable by the convention tests and overridable by host apps. " +
            "Packages whose tags are computed dynamically at runtime may be listed in exemptPackageNames. " +
            $"{violations.Count} violation(s):" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal)));
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Internals
    // ────────────────────────────────────────────────────────────────────────────

    private static List<TagNameDefault> ScanTagNameDefaults(string srcDir)
    {
        List<TagNameDefault> results = [];

        foreach (string file in Directory.GetFiles(srcDir, "*EndpointsOptions.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(file))
            {
                continue;
            }

            foreach (string line in File.ReadLines(file))
            {
                Match match = TagNameDeclaration().Match(line);
                if (match.Success)
                {
                    results.Add(new TagNameDefault(match.Groups[1].Value, match.Groups[2].Value, file));
                }
            }
        }

        return results;
    }

    private static string[] SplitOnSpaceDashSpace(string tag)
    {
        if (tag.Contains("  ", StringComparison.Ordinal))
        {
            return [tag];
        }

        int idx = tag.IndexOf(" - ", StringComparison.Ordinal);
        return idx < 0 ? [tag] : [tag[..idx], tag[(idx + 3)..]];
    }

    private static bool IsTitleCasePhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
        {
            return false;
        }

        return phrase.Split(' ').All(t => TitleCaseToken().IsMatch(t));
    }

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    /// <summary>Matches <c>private const string TagName</c> in endpoint implementation files.</summary>
    [GeneratedRegex(@"\bprivate\s+const\s+string\s+TagName\b")]
    private static partial Regex HardcodedTagNameConstant();

    /// <summary>
    /// Matches lines like <c>public string TagName { get; set; } = "Foo - Bar";</c>.
    /// Group 1 = property name, Group 2 = default value.
    /// </summary>
    [GeneratedRegex(@"public\s+string\s+(\w*TagName)\s*\{\s*get;\s*set;\s*\}\s*=\s*""([^""]+)""\s*;")]
    private static partial Regex TagNameDeclaration();

    /// <summary>Title-case token: initial cap + lowercase/digit run, or fully-uppercase acronym.</summary>
    [GeneratedRegex(@"^([A-Z][a-z0-9]*|[A-Z]{2,}[A-Z0-9]*)(-([A-Z][a-z0-9]*|[A-Z]{2,}[A-Z0-9]*))*$")]
    private static partial Regex TitleCaseToken();

    private sealed record TagNameDefault(string PropertyName, string Value, string FilePath);
}
