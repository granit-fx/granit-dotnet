using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Timeline;
using Granit.Timeline.Abstractions;
using Granit.Timeline.AI.Diagnostics;
using Granit.Timeline.AI.Internal;
using Granit.Timeline.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Timeline.AI.Tests;

public sealed class LlmTimelineAnomalyDetectorTests
{
    private static readonly Guid TestEntityId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ITimelineReader _timelineReader = Substitute.For<ITimelineReader>();
    private readonly IOptions<TimelineAIOptions> _options = MsOptions.Create(new TimelineAIOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly TimelineAIMetrics _metrics = CreateTestMetrics();

    public LlmTimelineAnomalyDetectorTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);
    }

    private LlmTimelineAnomalyDetector CreateSut() =>
        new(_chatClientFactory, _timelineReader, _options, _currentTenant, _metrics, NullLogger<LlmTimelineAnomalyDetector>.Instance);

    private static TimelineAIMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new TimelineAIMetrics(factory);
    }

    private static List<TimelineStreamEntry> MakeEntries(int count)
    {
        var entries = new List<TimelineStreamEntry>();
        for (int i = 0; i < count; i++)
        {
            entries.Add(new TimelineStreamEntry
            {
                Id = Guid.NewGuid(),
                OccurredAt = new DateTimeOffset(2026, 3, 16, 12, 0, 0, TimeSpan.Zero).AddHours(-i),
                EntryType = TimelineStreamEntryType.Comment,
                AuthorName = $"User {i}",
                Body = $"Entry body {i}",
            });
        }

        return entries;
    }

    [Fact]
    public async Task DetectAnomaliesAsync_NoEntries_ReturnsNoAnomalies()
    {
        LlmTimelineAnomalyDetector sut = CreateSut();

        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([], 0, false, null), []));

        AnomalyReport result = await sut.DetectAnomaliesAsync(
            "Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectAnomaliesAsync_AnomaliesFound_ReturnsReport()
    {
        LlmTimelineAnomalyDetector sut = CreateSut();
        List<TimelineStreamEntry> entries = MakeEntries(5);

        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>(entries, 5, false, null), []));

        string json = """{"anomalies": [{"description": "Bulk edits detected: 5 entries in 4 hours", "severity": "Medium"}, {"description": "Off-hours activity at 03:00 UTC", "severity": "Low"}]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        AnomalyReport result = await sut.DetectAnomaliesAsync(
            "Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies.Count.ShouldBe(2);
        result.Anomalies[0].Description.ShouldContain("Bulk edits");
        result.Anomalies[0].Severity.ShouldBe("Medium");
        result.Anomalies[1].Severity.ShouldBe("Low");
    }

    [Fact]
    public async Task DetectAnomaliesAsync_LlmFailure_ReturnsNoAnomalies()
    {
        LlmTimelineAnomalyDetector sut = CreateSut();
        List<TimelineStreamEntry> entries = MakeEntries(2);

        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>(entries, 2, false, null), []));

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        AnomalyReport result = await sut.DetectAnomaliesAsync(
            "Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAnomalyResponse_ValidJson_ReturnsAnomalies()
    {
        string json = """{"anomalies": [{"description": "Privilege escalation pattern", "severity": "High"}]}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies.Count.ShouldBe(1);
        result.Anomalies[0].Description.ShouldBe("Privilege escalation pattern");
        result.Anomalies[0].Severity.ShouldBe("High");
    }

    [Fact]
    public void ParseAnomalyResponse_EmptyAnomalies_ReturnsNoAnomalies()
    {
        string json = """{"anomalies": []}""";

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAnomalyResponse_InvalidJson_ReturnsNoAnomalies()
    {
        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse("not valid json");

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAnomalyResponse_MarkdownFencedJson_ReturnsAnomalies()
    {
        string json = """
            ```json
            {"anomalies": [{"description": "Rapid state changes", "severity": "medium"}]}
            ```
            """;

        AnomalyReport result = LlmTimelineAnomalyDetector.ParseAnomalyResponse(json);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies[0].Severity.ShouldBe("Medium");
    }

    [Fact]
    public void BuildAnomalyDetectionPrompt_ContainsEntityTypeAndEntries()
    {
        List<TimelineStreamEntry> entries = MakeEntries(2);

        string prompt = LlmTimelineAnomalyDetector.BuildAnomalyDetectionPrompt("Ticket", TestEntityId, entries);

        prompt.ShouldContain("Ticket");
        prompt.ShouldContain(TestEntityId.ToString());
        prompt.ShouldContain("Entry body 0");
        prompt.ShouldContain("bulk edits");
    }
}
