using Granit.Observability.AI.Internal;
using Shouldly;

namespace Granit.Observability.AI.Tests;

public sealed class LlmLogAnalyzerParseTests
{
    [Fact]
    public void ParseReport_ValidJson_ReturnsParsedReport()
    {
        const string json = """
            {
              "summary": "3 errors detected",
              "insights": [
                { "description": "Memory leak", "severity": "Critical", "category": "Anomaly" },
                { "description": "Slow queries", "severity": "Medium", "category": "Performance" }
              ]
            }
            """;

        LogAnalysisReport report = LlmLogAnalyzer.ParseReport(json, 10);

        report.Summary.ShouldBe("3 errors detected");
        report.Insights.Count.ShouldBe(2);
        report.Insights[0].Description.ShouldBe("Memory leak");
        report.Insights[0].Severity.ShouldBe("Critical");
        report.Insights[0].Category.ShouldBe("Anomaly");
        report.Insights[1].Description.ShouldBe("Slow queries");
        report.TotalEntries.ShouldBe(10);
    }

    [Fact]
    public void ParseReport_NullFields_UsesDefaults()
    {
        const string json = """
            {
              "summary": null,
              "insights": [{ "description": null, "severity": null, "category": null }]
            }
            """;

        LogAnalysisReport report = LlmLogAnalyzer.ParseReport(json, 5);

        report.Summary.ShouldBe("No summary available.");
        report.Insights.Count.ShouldBe(1);
        report.Insights[0].Description.ShouldBe("Unknown");
        report.Insights[0].Severity.ShouldBe("Medium");
        report.Insights[0].Category.ShouldBe("Pattern");
    }

    [Fact]
    public void ParseReport_InvalidJson_ReturnsFallback()
    {
        const string badJson = "not json {{{";

        LogAnalysisReport report = LlmLogAnalyzer.ParseReport(badJson, 7);

        report.Summary.ShouldBe("AI analysis returned a non-JSON response.");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(7);
    }

    [Fact]
    public void ParseReport_EmptyInsightsArray_ReturnsEmptyList()
    {
        const string json = """{"summary": "All clear", "insights": []}""";

        LogAnalysisReport report = LlmLogAnalyzer.ParseReport(json, 20);

        report.Summary.ShouldBe("All clear");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(20);
    }

    [Fact]
    public void BuildPrompt_IncludesAllEntries()
    {
        List<LogEntry> entries =
        [
            new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "Error", "Something failed", "System.Exception: boom"),
            new(DateTimeOffset.Parse("2026-01-01T12:01:00Z"), "Information", "All good", null),
        ];

        string prompt = LlmLogAnalyzer.BuildPrompt(entries);

        prompt.ShouldContain("Something failed");
        prompt.ShouldContain("All good");
        prompt.ShouldContain("Exception: System.Exception: boom");
        // Only one "  Exception:" prefix line should appear (for the entry that has an exception)
        int exceptionPrefixCount = prompt.Split("  Exception:").Length - 1;
        exceptionPrefixCount.ShouldBe(1);
    }

    [Fact]
    public void BuildPrompt_NoException_OmitsExceptionLine()
    {
        List<LogEntry> entries =
        [
            new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "Information", "Normal log", null),
        ];

        string prompt = LlmLogAnalyzer.BuildPrompt(entries);

        prompt.ShouldContain("Normal log");
        prompt.ShouldNotContain("Exception:");
    }
}
