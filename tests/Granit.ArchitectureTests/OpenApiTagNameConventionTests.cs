using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates the OpenAPI tag-name convention from CLAUDE.md
/// (<i>"OpenAPI tags — naming convention (STRICT)"</i>): every tag default
/// declared on a <c>*EndpointsOptions</c> class must match either
/// <c>Title Case With Spaces</c> (single-tag modules — <c>"Blob Storage"</c>,
/// <c>"Privacy"</c>) or the <c>&lt;Module&gt; - &lt;SubGroup&gt;</c> compound
/// (multi-tag modules — <c>"Dashboards - Catalogue"</c>, <c>"AI - Inference"</c>).
/// </summary>
/// <remarks>
/// <para>
/// The dash compound uses <i>space-dash-space</i> exactly. Glued PascalCase
/// (<c>"BlobStorage"</c>), kebab-case (<c>"blob-storage"</c>), or any tag with
/// a lowercase first letter on either side of the dash is rejected — those
/// look ugly in Scalar's left column and break alphabetical sub-grouping by
/// module prefix.
/// </para>
/// <para>
/// The token regex tolerates internal hyphens within a single word (e.g.
/// <c>"Two-Factor"</c>) and treats fully-uppercase acronyms as valid tokens
/// (<c>"AI"</c>, <c>"OIDC"</c>, <c>"API"</c>). It does <b>not</b> tolerate
/// trailing or leading whitespace.
/// </para>
/// </remarks>
public sealed partial class OpenApiTagNameConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void All_TagName_defaults_should_follow_TitleCase_or_module_subgroup_format()
    {
        IReadOnlyList<TagNameDefault> defaults = ScanTagNameDefaults();
        defaults.ShouldNotBeEmpty(
            "No TagName defaults discovered — the test cannot run. Ensure the *.Endpoints " +
            "modules under src/ ship their EndpointsOptions classes with public string TagName defaults.");

        List<string> violations = [];

        foreach (TagNameDefault entry in defaults)
        {
            if (!IsValidTagName(entry.Value))
            {
                string relative = Path.GetRelativePath(RepoRoot, entry.FilePath);
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

    [Theory]
    [InlineData("Privacy")]
    [InlineData("Blob Storage")]
    [InlineData("Background Jobs")]
    [InlineData("Customer Balance")]
    [InlineData("Reference Data")]
    [InlineData("API Keys")]
    [InlineData("OIDC Admin")]
    [InlineData("AI - Inference")]
    [InlineData("AI - Workspaces")]
    [InlineData("Account - Login")]
    [InlineData("Account - Two-Factor")]
    [InlineData("Notifications - Mobile Push")]
    [InlineData("Dashboards - Catalogue")]
    [InlineData("Dashboards - Instances")]
    [InlineData("Dashboards - Widgets")]
    [InlineData("Identity - User Cache")]
    public void Valid_TagName_examples_are_accepted(string tag)
        => IsValidTagName(tag).ShouldBeTrue($"'{tag}' should be a valid tag name");

    [Theory]
    [InlineData("")]                                  // empty
    [InlineData(" Privacy")]                          // leading space
    [InlineData("Privacy ")]                          // trailing space
    [InlineData("BlobStorage")]                       // glued PascalCase
    [InlineData("blob storage")]                      // lowercase first letter
    [InlineData("blob-storage")]                      // kebab-case
    [InlineData("Blob_Storage")]                      // snake_case
    [InlineData("Dashboards-Catalogue")]              // missing spaces around dash
    [InlineData("dashboards - Catalogue")]            // lowercase module
    [InlineData("Dashboards - catalogue")]            // lowercase subgroup
    [InlineData("Dashboards -")]                      // empty subgroup
    [InlineData("- Catalogue")]                       // empty module
    [InlineData("Dashboards  -  Catalogue")]          // double spaces
    public void Invalid_TagName_examples_are_rejected(string tag)
        => IsValidTagName(tag).ShouldBeFalse($"'{tag}' should be rejected as a tag name");

    /// <summary>
    /// A tag is valid when, after splitting on the optional <c>" - "</c> separator,
    /// every resulting segment is a Title-Case phrase composed of one or more
    /// space-separated tokens, each of which starts with an uppercase letter or
    /// is a fully-uppercase acronym. Internal hyphens within a token (e.g.
    /// <c>"Two-Factor"</c>) are allowed only on the SubGroup side of a
    /// <c>&lt;Module&gt; - &lt;SubGroup&gt;</c> compound — a single-segment tag may
    /// not contain a hyphen, otherwise <c>"Dashboards-Catalogue"</c> would be
    /// indistinguishable from a missing-spaces typo of
    /// <c>"Dashboards - Catalogue"</c>.
    /// </summary>
    internal static bool IsValidTagName(string tag)
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

    private static string[] SplitOnSpaceDashSpace(string tag)
    {
        // Reject double spaces around the separator before splitting so the regex doesn't have
        // to model every malformed variant.
        if (tag.Contains("  ", StringComparison.Ordinal))
        {
            return [tag]; // forces caller to fall through to the strict phrase check, which then fails
        }

        int idx = tag.IndexOf(" - ", StringComparison.Ordinal);
        if (idx < 0)
        {
            return [tag];
        }

        string left = tag[..idx];
        string right = tag[(idx + 3)..];
        return [left, right];
    }

    private static bool IsTitleCasePhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
        {
            return false;
        }

        string[] tokens = phrase.Split(' ');
        return tokens.All(t => TitleCaseToken().IsMatch(t));
    }

    /// <summary>
    /// A token is either Title Case (initial cap + lowercase / digit run) or a
    /// fully-uppercase acronym of two or more letters. Hyphenated continuations
    /// follow the same shape, so <c>"Two-Factor"</c> is accepted but
    /// <c>"BlobStorage"</c> (glued PascalCase) is rejected because the second
    /// capital after a lowercase run never matches.
    /// Examples: <c>"Privacy"</c>, <c>"OIDC"</c>, <c>"Two-Factor"</c>, <c>"API"</c>, <c>"AI"</c>.
    /// </summary>
    [GeneratedRegex(@"^([A-Z][a-z0-9]*|[A-Z]{2,}[A-Z0-9]*)(-([A-Z][a-z0-9]*|[A-Z]{2,}[A-Z0-9]*))*$")]
    private static partial Regex TitleCaseToken();

    /// <summary>
    /// Matches lines like:
    /// <code>public string CatalogTagName { get; set; } = "Dashboards - Catalogue";</code>
    /// Group 1: property name (e.g. <c>CatalogTagName</c>),
    /// Group 2: default value (e.g. <c>Dashboards - Catalogue</c>).
    /// </summary>
    [GeneratedRegex(@"public\s+string\s+(\w*TagName)\s*\{\s*get;\s*set;\s*\}\s*=\s*""([^""]+)""\s*;")]
    private static partial Regex TagNameDeclaration();

    private static List<TagNameDefault> ScanTagNameDefaults()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<TagNameDefault> results = [];

        foreach (string optionsFile in Directory.GetFiles(srcDir, "*EndpointsOptions.cs", SearchOption.AllDirectories))
        {
            if (optionsFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || optionsFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            foreach (string line in File.ReadLines(optionsFile))
            {
                Match match = TagNameDeclaration().Match(line);
                if (match.Success)
                {
                    results.Add(new TagNameDefault(
                        match.Groups[1].Value,
                        match.Groups[2].Value,
                        optionsFile));
                }
            }
        }

        return results;
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(OpenApiTagNameConventionTests).Assembly.Location);
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

    private sealed record TagNameDefault(string PropertyName, string Value, string FilePath);
}
