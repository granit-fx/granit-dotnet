using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces D5 (#1569) — widget runner impls MUST live in
/// <c>Granit.Analytics.EntityFrameworkCore</c>, NOT in
/// <c>Granit.Analytics.Endpoints</c>. The HTTP layer stays focused on
/// routing / DTO shape / OpenAPI metadata; the runtime execution layer
/// (which is EF Core-coupled via <c>IQueryEngine.ExecuteStreamAsync</c>)
/// must be reachable by non-HTTP consumers (background jobs, push
/// transports, future workers) without referencing <c>.Endpoints</c>.
/// </summary>
/// <remarks>
/// <para>
/// Mirror of the CLAUDE.md "Declarative definitions placement (STRICT)"
/// rule: just as concrete <c>QueryDefinition</c> / <c>ExportDefinition</c>
/// classes live in the base module and not <c>.Endpoints</c>, the widget
/// runner impls live in <c>.EntityFrameworkCore</c> and not in
/// <c>.Endpoints</c>. This test fails the build when a new runner is
/// accidentally added to the wrong package.
/// </para>
/// <para>
/// Naming convention: any concrete class whose name ends with
/// <c>Runner</c> AND lives under namespace
/// <c>Granit.Analytics.Endpoints.Internal</c> is a violation. The
/// metric-endpoint orchestration types (<c>MetricRunner</c> + matching
/// service) are exempt because they live in
/// <c>Granit.Analytics.Endpoints/Internal/</c> and orchestrate caching
/// + period comparison, both HTTP-adjacent concerns. The exemption is
/// listed inline so a future relocation is a one-line edit.
/// </para>
/// </remarks>
public sealed class AnalyticsRunnerPlacementTests
{
    /// <summary>HTTP-adjacent types in <c>Granit.Analytics.Endpoints</c> that legitimately end in <c>Runner</c> — exempt from the placement rule.</summary>
    private static readonly HashSet<string> EndpointsRunnerExemptions = new(StringComparer.Ordinal)
    {
        // MetricRunner orchestrates the metric ENDPOINT — caching + period
        // comparison + JSON shape. Per-tenant caching key + period token
        // resolution are HTTP-shape concerns, distinct from the streaming
        // aggregation handled by MetricExecutor in .EntityFrameworkCore.
        "MetricRunner`2",
    };

    [Fact]
    public void Endpoints_NoRunnerImpls_ExceptExemptions()
    {
        Assembly endpoints = LoadFrameworkAssemblyOrSkip("Granit.Analytics.Endpoints");

        IReadOnlyList<Type> offenders = [.. endpoints.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Name.EndsWith("Runner", StringComparison.Ordinal)
                || (t.IsGenericTypeDefinition && t.Name.Contains("Runner`", StringComparison.Ordinal)))
            .Where(t => !EndpointsRunnerExemptions.Contains(t.Name))];

        offenders.ShouldBeEmpty(
            "D5 (#1569): widget runner impls MUST live in Granit.Analytics.EntityFrameworkCore, " +
            "not in Granit.Analytics.Endpoints. Move the offending types to " +
            "src/Granit.Analytics.EntityFrameworkCore/Internal/. Exemptions live in " +
            $"{nameof(AnalyticsRunnerPlacementTests)}.{nameof(EndpointsRunnerExemptions)} with a one-line justification each." +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders.Select(t => $"  - {t.FullName}")));
    }

    [Fact]
    public void EntityFrameworkCore_HostsTheExpectedRunners()
    {
        // Inverse rule — pin the new placement so a regression that moves a
        // runner BACK to .Endpoints fails immediately.
        Assembly efc = LoadFrameworkAssemblyOrSkip("Granit.Analytics.EntityFrameworkCore");

        HashSet<string> runnerNames = [.. efc.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.Contains("Runner`", StringComparison.Ordinal))
            .Select(t => t.Name)];

        runnerNames.ShouldContain("ChartRunner`1");
        runnerNames.ShouldContain("TableRunner`1");
        runnerNames.ShouldContain("PivotRunner`1");
        runnerNames.ShouldContain("MapRunner`1");
        runnerNames.ShouldContain("QueryAggregateRunner`1");
    }

    private static Assembly LoadFrameworkAssemblyOrSkip(string simpleName)
    {
        string outputDir = Path.GetDirectoryName(typeof(AnalyticsRunnerPlacementTests).Assembly.Location)!;
        string path = Path.Combine(outputDir, $"{simpleName}.dll");

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Expected '{path}' next to the architecture test assembly. The framework build is incomplete.");
        }

        return Assembly.LoadFrom(path);
    }
}
