using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that metrics tag keys do not expose PII-indicative names.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class MetricsPiiConventionTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(MetricsPiiConventionTests).Assembly);

    [Fact]
    public void Metrics_tags_should_not_contain_pii_names() =>
        PiiConventionRules.MetricsTagsShouldNotContainPiiNames(
            Path.Join(RepoRoot, "src"),
            RepoRoot);
}
