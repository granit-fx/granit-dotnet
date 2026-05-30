using Granit.Timeline.AI.Internal;
using Shouldly;
using Xunit;
using AnomalyItemJson = Granit.Timeline.AI.Internal.LlmTimelineAnomalyDetector.AnomalyItemJson;
using AnomalyResponseJson = Granit.Timeline.AI.Internal.LlmTimelineAnomalyDetector.AnomalyResponseJson;

namespace Granit.Timeline.AI.Tests;

/// <summary>
/// Severity normalization and description filtering — now exercised through
/// <see cref="LlmTimelineAnomalyDetector.BuildReport"/> (JSON parsing/fence-stripping is the
/// primitive's responsibility).
/// </summary>
public sealed class ParseAnomalyResponseAdditionalTests
{
    private static AnomalyReport Build(string? description, string? severity) =>
        LlmTimelineAnomalyDetector.BuildReport(new AnomalyResponseJson([new AnomalyItemJson(description, severity)]));

    [Fact]
    public void BuildReport_NullSeverity_DefaultsToLow() =>
        Build("Something odd", null).Anomalies[0].Severity.ShouldBe("Low");

    [Fact]
    public void BuildReport_UnknownSeverity_DefaultsToLow() =>
        Build("Something odd", "Critical").Anomalies[0].Severity.ShouldBe("Low");

    [Fact]
    public void BuildReport_HighSeverity_NormalizedCorrectly() =>
        Build("Privilege escalation", "high").Anomalies[0].Severity.ShouldBe("High");

    [Fact]
    public void BuildReport_MediumSeverity_NormalizedCorrectly() =>
        Build("Bulk edits", "MEDIUM").Anomalies[0].Severity.ShouldBe("Medium");

    [Fact]
    public void BuildReport_EmptyDescription_Filtered()
    {
        AnomalyReport result = LlmTimelineAnomalyDetector.BuildReport(new AnomalyResponseJson(
        [
            new AnomalyItemJson("", "Low"),
            new AnomalyItemJson("Valid", "High"),
        ]));

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies.Count.ShouldBe(1);
        result.Anomalies[0].Description.ShouldBe("Valid");
    }

    [Fact]
    public void BuildReport_WhitespaceDescription_Filtered()
    {
        AnomalyReport result = Build("   ", "Low");

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void BuildReport_NullAnomaliesProperty_ReturnsNoAnomalies() =>
        LlmTimelineAnomalyDetector.BuildReport(new AnomalyResponseJson(null)).HasAnomalies.ShouldBeFalse();

    [Fact]
    public void BuildReport_MultipleAnomalies_AllParsed()
    {
        AnomalyReport result = LlmTimelineAnomalyDetector.BuildReport(new AnomalyResponseJson(
        [
            new AnomalyItemJson("A", "Low"),
            new AnomalyItemJson("B", "Medium"),
            new AnomalyItemJson("C", "High"),
        ]));

        result.Anomalies.Count.ShouldBe(3);
    }
}
