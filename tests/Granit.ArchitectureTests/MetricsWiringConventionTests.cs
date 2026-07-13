using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Every <c>{Module}Metrics</c> class must be consumed — constructor-injected or resolved —
/// by at least one type in <c>src/</c>. The 2026-07 Http* audit found a metrics class that
/// was DI-registered, fully unit-tested, and never injected: its counters never emitted a
/// single data point while dashboards assumed they existed.
/// </summary>
public sealed partial class MetricsWiringConventionTests
{
    private static readonly string RepoRoot = ArchitectureTestHelpers.FindRepoRoot();

    /// <summary>
    /// Ratchet list — nothing may be added without a linked issue. Key: metrics type name.
    /// </summary>
    private static readonly Dictionary<string, string> Exemptions = new(StringComparer.Ordinal)
    {
        // Pre-existing repo-wide debt, enumerated at introduction — tracked by #3010.
        ["ApiKeysMetrics"] = "#3010 — registered singleton, counters never recorded",
        ["TemplatingMetrics"] = "#3010 — registered singleton, counters never recorded",
    };

    [Fact]
    public void Every_metrics_class_is_consumed_somewhere_in_src()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        List<(string File, string TypeName)> metricsClasses = [];
        foreach (string csFile in ArchitectureTestHelpers.EnumerateSourceFiles(srcDir))
        {
            Match match = MetricsClassDeclaration().Match(File.ReadAllText(csFile));
            if (match.Success)
            {
                metricsClasses.Add((csFile, match.Groups[1].Value));
            }
        }

        string allSources = string.Join('\n', ArchitectureTestHelpers.EnumerateSourceFiles(srcDir)
            .Select(File.ReadAllText));

        foreach ((string file, string typeName) in metricsClasses)
        {
            if (Exemptions.ContainsKey(typeName))
            {
                continue;
            }

            // Consumption = the type name appears somewhere OTHER than its declaration,
            // registration, or a typeof() reference: constructor parameter
            // "(XxxMetrics metrics" / "XxxMetrics metrics," or service resolution.
            bool consumed =
                ConsumptionSites(typeName).Any(pattern => allSources.Contains(pattern, StringComparison.Ordinal));

            if (!consumed)
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, file)} ({typeName})");
            }
        }

        violations.ShouldBeEmpty(
            "Every {Module}Metrics class must be constructor-injected (or resolved) by at "
            + "least one production type — a registered-but-never-injected metrics class "
            + "emits nothing while dashboards assume it does. Wire it or add a justified "
            + "exemption. Violators: " + string.Join("; ", violations));
    }

    private static IEnumerable<string> ConsumptionSites(string typeName)
    {
        // Constructor / method parameter (primary ctor or classic, required or
        // optional-nullable), field initialization, or explicit resolution.
        yield return $"({typeName} ";
        yield return $"({typeName}? ";
        yield return $" {typeName} metrics";
        yield return $" {typeName}? metrics";
        yield return $", {typeName} ";
        yield return $", {typeName}? ";
        yield return $"GetRequiredService<{typeName}>";
        yield return $"GetService<{typeName}>";
    }

    /// <summary>Matches <c>class XxxMetrics</c> declarations under a <c>Diagnostics</c> namespace or folder.</summary>
    [GeneratedRegex(@"(?:public|internal)\s+sealed\s+(?:partial\s+)?class\s+([A-Za-z0-9_]+Metrics)\b")]
    private static partial Regex MetricsClassDeclaration();
}
