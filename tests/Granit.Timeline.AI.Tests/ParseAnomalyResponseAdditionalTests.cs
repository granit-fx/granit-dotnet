using Granit.Timeline.AI.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class ParseAnomalyResponseAdditionalTests
{
    [Fact]
    public void ParseAnomalyResponse_NullSeverity_DefaultsToLow()
    {
        string json = """{"anomalies": [{"description": "Something odd", "severity": null}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies[0].Severity.ShouldBe("Low");
    }

    [Fact]
    public void ParseAnomalyResponse_UnknownSeverity_DefaultsToLow()
    {
        string json = """{"anomalies": [{"description": "Something odd", "severity": "Critical"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies[0].Severity.ShouldBe("Low");
    }

    [Fact]
    public void ParseAnomalyResponse_HighSeverity_NormalizedCorrectly()
    {
        string json = """{"anomalies": [{"description": "Privilege escalation", "severity": "high"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.Anomalies[0].Severity.ShouldBe("High");
    }

    [Fact]
    public void ParseAnomalyResponse_LowSeverity_NormalizedCorrectly()
    {
        string json = """{"anomalies": [{"description": "Minor issue", "severity": "low"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.Anomalies[0].Severity.ShouldBe("Low");
    }

    [Fact]
    public void ParseAnomalyResponse_MediumSeverity_NormalizedCorrectly()
    {
        string json = """{"anomalies": [{"description": "Bulk edits", "severity": "MEDIUM"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.Anomalies[0].Severity.ShouldBe("Medium");
    }

    [Fact]
    public void ParseAnomalyResponse_EmptyDescription_Filtered()
    {
        string json = """{"anomalies": [{"description": "", "severity": "Low"}, {"description": "Valid", "severity": "High"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies.Count.ShouldBe(1);
        result.Anomalies[0].Description.ShouldBe("Valid");
    }

    [Fact]
    public void ParseAnomalyResponse_WhitespaceDescription_Filtered()
    {
        string json = """{"anomalies": [{"description": "   ", "severity": "Low"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAnomalyResponse_NullAnomaliesProperty_ReturnsNoAnomalies()
    {
        string json = """{"anomalies": null}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeFalse();
    }

    [Fact]
    public void ParseAnomalyResponse_MultipleAnomalies_AllParsed()
    {
        string json = """{"anomalies": [{"description": "A", "severity": "Low"}, {"description": "B", "severity": "Medium"}, {"description": "C", "severity": "High"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.Anomalies.Count.ShouldBe(3);
    }
}
