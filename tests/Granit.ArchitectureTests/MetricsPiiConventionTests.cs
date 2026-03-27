using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that metrics tag values in <c>*Metrics.cs</c> files do not contain
/// PII-indicative property names. Using user-controlled values as metric tags
/// causes both a PII leak and a cardinality explosion (unbounded memory).
/// </summary>
/// <remarks>
/// Scans all <c>TagList</c> initializers for string literals matching PII patterns
/// (email, phone, name, IP address, etc.). Only bounded, low-cardinality tag
/// values should appear (tenant_id, category, status, operation, grant_type).
/// </remarks>
public sealed partial class MetricsPiiConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Allowed tag names — bounded, low-cardinality values that are safe for metrics.
    /// </summary>
    private static readonly HashSet<string> AllowedTagNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenant_id",
        "category",
        "status",
        "operation",
        "grant_type",
        "reason",
        "provider",
        "action",
        "tool_name",
        "resource_name",
        "event_type",
        "entity_type",
        "change_type",
        "is_new_user",
        "keys_generated",
        "keys_retired",
        "keys_revoked",
        "channel",
        "format",
        "template_type",
        "culture",
        "data_type",
        "job_type",
        "direction",
        "method",
        "policy",
        "state",
        "module",
        "severity",
        "analysis_type",
        "feature_name",
    };

    /// <summary>
    /// Metric tag keys must not use PII-indicative names (GDPR Art. 5, ISO 27001 A.8.2).
    /// Tags like <c>email</c>, <c>userName</c>, or <c>ipAddress</c> would cause both
    /// a privacy leak and a cardinality explosion.
    /// </summary>
    [Fact]
    public void Metrics_tags_should_not_contain_pii_names()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetMetricsFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);
            int lineNumber = 0;

            foreach (string line in File.ReadLines(csFile))
            {
                lineNumber++;

                // Match tag key literals in TagList: { "key_name", value }
                foreach (Match match in TagKeyLiteral().Matches(line))
                {
                    string tagName = match.Groups[1].Value;

                    if (AllowedTagNames.Contains(tagName))
                    {
                        continue;
                    }

                    if (PiiTagName().IsMatch(tagName))
                    {
                        violations.Add($"{relativePath}:{lineNumber} tag \"{tagName}\"");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Metric tags must not contain PII-indicative names (GDPR Art. 5 — data minimization, " +
            "cardinality explosion risk). Use bounded, low-cardinality values only. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    private static IEnumerable<string> GetMetricsFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*Metrics.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(MetricsPiiConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Join(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches string literal keys in TagList initializers: <c>{ "key_name", ... }</c>.
    /// Group 1: the tag key name.
    /// </summary>
    [GeneratedRegex(@"\{\s*""(\w+)""\s*,", RegexOptions.None)]
    private static partial Regex TagKeyLiteral();

    /// <summary>
    /// Tag names that indicate PII — must never appear as metric tags.
    /// </summary>
    [GeneratedRegex(
        @"(?i)(email|mail|phone|mobile|address|street|city|postal|zip|firstName|lastName|fullName|displayName|userName|username|ssn|nationalId|passport|birthDate|dateOfBirth|salary|income|bankAccount|iban|ipAddress|password|secret|token|avatar|photo)",
        RegexOptions.None)]
    private static partial Regex PiiTagName();
}
