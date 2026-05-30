using Granit.Observability.AI.Internal;
using Shouldly;
using LlmAnalysisResponse = Granit.Observability.AI.Internal.LlmLogAnalyzer.LlmAnalysisResponse;
using LlmInsightResponse = Granit.Observability.AI.Internal.LlmLogAnalyzer.LlmInsightResponse;

namespace Granit.Observability.AI.Tests;

/// <summary>
/// Report mapping and entry-block building. JSON parsing / fence-stripping is now the
/// primitive's responsibility, so those tests are exercised through
/// <see cref="LlmLogAnalyzer.BuildReport"/> and <see cref="LlmLogAnalyzer.BuildEntriesBlock"/>.
/// </summary>
public sealed class LlmLogAnalyzerParseTests
{
    [Fact]
    public void BuildReport_ValidResponse_ReturnsReport()
    {
        LogAnalysisReport report = LlmLogAnalyzer.BuildReport(new LlmAnalysisResponse
        {
            Summary = "3 errors detected",
            Insights =
            [
                new LlmInsightResponse { Description = "Memory leak", Severity = "Critical", Category = "Anomaly" },
                new LlmInsightResponse { Description = "Slow queries", Severity = "Medium", Category = "Performance" },
            ],
        }, 10);

        report.Summary.ShouldBe("3 errors detected");
        report.Insights.Count.ShouldBe(2);
        report.Insights[0].Description.ShouldBe("Memory leak");
        report.Insights[0].Severity.ShouldBe("Critical");
        report.Insights[0].Category.ShouldBe("Anomaly");
        report.TotalEntries.ShouldBe(10);
    }

    [Fact]
    public void BuildReport_NullFields_UsesDefaults()
    {
        LogAnalysisReport report = LlmLogAnalyzer.BuildReport(new LlmAnalysisResponse
        {
            Summary = null,
            Insights = [new LlmInsightResponse { Description = null, Severity = null, Category = null }],
        }, 5);

        report.Summary.ShouldBe("No summary available.");
        report.Insights.Count.ShouldBe(1);
        report.Insights[0].Description.ShouldBe("Unknown");
        report.Insights[0].Severity.ShouldBe("Medium");
        report.Insights[0].Category.ShouldBe("Pattern");
    }

    [Fact]
    public void BuildReport_NullInsights_ReturnsEmptyList()
    {
        LogAnalysisReport report = LlmLogAnalyzer.BuildReport(new LlmAnalysisResponse { Summary = "All clear", Insights = null }, 20);

        report.Summary.ShouldBe("All clear");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(20);
    }

    [Fact]
    public void BuildEntriesBlock_IncludesAllEntries()
    {
        List<LogEntry> entries =
        [
            new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "Error", "Something failed", "System.Exception: boom"),
            new(DateTimeOffset.Parse("2026-01-01T12:01:00Z"), "Information", "All good", null),
        ];

        string block = LlmLogAnalyzer.BuildEntriesBlock(entries);

        block.ShouldContain("Something failed");
        block.ShouldContain("All good");
        block.ShouldContain("Exception: System.Exception: boom");
        (block.Split("  Exception:").Length - 1).ShouldBe(1);
    }

    [Fact]
    public void BuildEntriesBlock_NoException_OmitsExceptionLine()
    {
        string block = LlmLogAnalyzer.BuildEntriesBlock(
            [new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "Information", "Normal log", null)]);

        block.ShouldContain("Normal log");
        block.ShouldNotContain("Exception:");
    }
}
