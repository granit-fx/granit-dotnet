using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.AI.Diagnostics;
using Granit.Timeline.AI.Internal;
using Granit.Timeline.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using AnomalyItemJson = Granit.Timeline.AI.Internal.LlmTimelineAnomalyDetector.AnomalyItemJson;
using AnomalyResponseJson = Granit.Timeline.AI.Internal.LlmTimelineAnomalyDetector.AnomalyResponseJson;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Timeline.AI.Tests;

public sealed class LlmTimelineAnomalyDetectorTests
{
    private static readonly Guid TestEntityId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly ITimelineReader _timelineReader = Substitute.For<ITimelineReader>();
    private readonly IOptions<TimelineAIOptions> _options = MsOptions.Create(new TimelineAIOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly TimelineAIMetrics _metrics = CreateTestMetrics();

    private LlmTimelineAnomalyDetector CreateSut() =>
        new(_structured, _timelineReader, _options, _currentTenant, _metrics, NullLogger<LlmTimelineAnomalyDetector>.Instance);

    private static TimelineAIMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new TimelineAIMetrics(factory);
    }

    private void HasEntries(int count) =>
        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>(MakeEntries(count), count, false, null), []));

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

    private void Completes(StructuredCompletionStatus status, AnomalyResponseJson? value = null) =>
        _structured
            .CompleteAsync<AnomalyResponseJson>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<AnomalyResponseJson> { Status = status, Value = value });

    [Fact]
    public async Task DetectAnomaliesAsync_NoEntries_ReturnsNoAnomalies()
    {
        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([], 0, false, null), []));

        AnomalyReport result = await CreateSut().DetectAnomaliesAsync("Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectAnomaliesAsync_AnomaliesFound_ReturnsReport()
    {
        HasEntries(5);
        Completes(StructuredCompletionStatus.Succeeded, new AnomalyResponseJson(
        [
            new AnomalyItemJson("Bulk edits detected: 5 entries in 4 hours", "Medium"),
            new AnomalyItemJson("Off-hours activity at 03:00 UTC", "Low"),
        ]));

        AnomalyReport result = await CreateSut().DetectAnomaliesAsync("Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies.Count.ShouldBe(2);
        result.Anomalies[0].Description.ShouldContain("Bulk edits");
        result.Anomalies[0].Severity.ShouldBe("Medium");
        result.Anomalies[1].Severity.ShouldBe("Low");
    }

    [Fact]
    public async Task DetectAnomaliesAsync_TransportFailure_ReturnsNoAnomalies()
    {
        HasEntries(2);
        Completes(StructuredCompletionStatus.TransportFailure);

        AnomalyReport result = await CreateSut().DetectAnomaliesAsync("Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectAnomaliesAsync_SchemaViolation_ReturnsNoAnomalies()
    {
        HasEntries(2);
        Completes(StructuredCompletionStatus.SchemaViolation);

        AnomalyReport result = await CreateSut().DetectAnomaliesAsync("Order", TestEntityId, TestContext.Current.CancellationToken);

        result.HasAnomalies.ShouldBeFalse();
    }

    [Fact]
    public async Task DetectAnomaliesAsync_PassesPseudonymizedEntriesAsContent()
    {
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = new DateTimeOffset(2026, 3, 16, 12, 0, 0, TimeSpan.Zero),
            EntryType = TimelineStreamEntryType.Comment,
            AuthorId = "abcdef1234567890",
            AuthorName = "Jane Doe",
            Body = "Did a thing",
        };
        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([entry], 1, false, null), []));

        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<AnomalyResponseJson>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<AnomalyResponseJson> { Status = StructuredCompletionStatus.Succeeded, Value = new AnomalyResponseJson([]) });

        await CreateSut().DetectAnomaliesAsync("Ticket", TestEntityId, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldNotBeNull();
        captured.Content.ShouldContain("Did a thing");
        captured.Content.ShouldContain("User-abcdef12"); // pseudonymized author
        captured.Content.ShouldNotContain("Jane Doe");   // raw author name never sent
        captured.Instruction!.ShouldContain("Ticket");
    }

    [Fact]
    public void BuildReport_ValidAnomalies_ReturnsReport()
    {
        AnomalyReport result = LlmTimelineAnomalyDetector.BuildReport(
            new AnomalyResponseJson([new AnomalyItemJson("Privilege escalation pattern", "High")]));

        result.HasAnomalies.ShouldBeTrue();
        result.Anomalies.Count.ShouldBe(1);
        result.Anomalies[0].Description.ShouldBe("Privilege escalation pattern");
        result.Anomalies[0].Severity.ShouldBe("High");
    }

    [Fact]
    public void BuildReport_EmptyAnomalies_ReturnsNoAnomalies()
    {
        AnomalyReport result = LlmTimelineAnomalyDetector.BuildReport(new AnomalyResponseJson([]));

        result.HasAnomalies.ShouldBeFalse();
        result.Anomalies.ShouldBeEmpty();
    }
}
