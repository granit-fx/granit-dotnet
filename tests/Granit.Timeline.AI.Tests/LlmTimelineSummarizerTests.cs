using System.Diagnostics.Metrics;
using Granit.AI;
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

public sealed class LlmTimelineSummarizerTests
{
    private static readonly Guid TestEntityId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ITimelineReader _timelineReader = Substitute.For<ITimelineReader>();
    private readonly IOptions<TimelineAIOptions> _options = MsOptions.Create(new TimelineAIOptions());
    private readonly TimelineAIMetrics _metrics = CreateTestMetrics();

    public LlmTimelineSummarizerTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);
    }

    private LlmTimelineSummarizer CreateSut() =>
        new(_chatClientFactory, _timelineReader, _options, _metrics, NullLogger<LlmTimelineSummarizer>.Instance);

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
    public async Task SummarizeAsync_NoEntries_ReturnsEmptySummary()
    {
        LlmTimelineSummarizer sut = CreateSut();

        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineStreamEntry>([], 0, false, null));

        TimelineSummary result = await sut.SummarizeAsync(
            "Order", TestEntityId, ct: TestContext.Current.CancellationToken);

        result.Text.ShouldBe("No timeline entries found.");
        result.EntryCount.ShouldBe(0);
        result.OldestEntry.ShouldBeNull();
        result.NewestEntry.ShouldBeNull();
    }

    [Fact]
    public async Task SummarizeAsync_WithEntries_ReturnsLlmSummary()
    {
        LlmTimelineSummarizer sut = CreateSut();
        List<TimelineStreamEntry> entries = MakeEntries(3);

        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineStreamEntry>(entries, 3, false, null));

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, "Three entries were posted by different users over 2 hours.")]));

        TimelineSummary result = await sut.SummarizeAsync(
            "Order", TestEntityId, ct: TestContext.Current.CancellationToken);

        result.Text.ShouldBe("Three entries were posted by different users over 2 hours.");
        result.EntryCount.ShouldBe(3);
        result.OldestEntry.ShouldNotBeNull();
        result.NewestEntry.ShouldNotBeNull();
        result.NewestEntry.Value.ShouldBeGreaterThanOrEqualTo(result.OldestEntry.Value);
    }

    [Fact]
    public async Task SummarizeAsync_LlmFailure_ReturnsFallbackSummary()
    {
        LlmTimelineSummarizer sut = CreateSut();
        List<TimelineStreamEntry> entries = MakeEntries(2);

        _timelineReader
            .GetStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineStreamEntry>(entries, 2, false, null));

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        TimelineSummary result = await sut.SummarizeAsync(
            "Order", TestEntityId, ct: TestContext.Current.CancellationToken);

        result.Text.ShouldBe("Summary generation failed.");
        result.EntryCount.ShouldBe(2);
    }

    [Fact]
    public void BuildSummarizationPrompt_ContainsEntityTypeAndEntries()
    {
        List<TimelineStreamEntry> entries = MakeEntries(2);

        string prompt = LlmTimelineSummarizer.BuildSummarizationPrompt("Ticket", TestEntityId, entries);

        prompt.ShouldContain("Ticket");
        prompt.ShouldContain(TestEntityId.ToString());
        prompt.ShouldContain("Entry body 0");
        prompt.ShouldContain("Entry body 1");
        prompt.ShouldContain("User 0");
    }
}
