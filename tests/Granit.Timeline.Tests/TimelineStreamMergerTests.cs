// =============================================================================
// Tests — TimelineStreamMerger
// =============================================================================
// Pins the federated read pipeline invariants:
//   - Shadow rows dedupe matching external entries by (SourceKey, SourceId)
//   - Canonical sort: OccurredAt DESC, native wins ties, SourceKey ASC, Id ASC
//   - MaxPage cap throws TimelineDepthExceededException
//   - DegradeGracefully drops failing sources and reports them
//   - ThrowAll propagates source failures
// =============================================================================

using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Exceptions;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineStreamMergerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);

    private static TimelineStreamEntry Native(int secondsOffset, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            OccurredAt = T0.AddSeconds(secondsOffset),
            EntryType = TimelineStreamEntryType.Comment,
            Body = $"native-{secondsOffset}",
            Origin = TimelineEntryOrigin.Native,
            SourceKey = TimelineSourceKeys.Native,
        };

    private static TimelineStreamEntry External(string sourceKey, string sourceId, int secondsOffset) =>
        new()
        {
            Id = Guid.NewGuid(),
            OccurredAt = T0.AddSeconds(secondsOffset),
            EntryType = TimelineStreamEntryType.SystemLog,
            Body = $"{sourceKey}-{sourceId}",
            Origin = TimelineEntryOrigin.External,
            SourceKey = sourceKey,
            SourceId = sourceId,
        };

    private static TimelineStreamEntry Shadow(string sourceKey, string sourceId, int secondsOffset) =>
        new()
        {
            Id = Guid.NewGuid(),
            OccurredAt = T0.AddSeconds(secondsOffset),
            EntryType = TimelineStreamEntryType.SystemLog,
            Body = $"shadow-of-{sourceId}",
            // Shadow lives in the native table but carries SourceKey to dedupe.
            Origin = TimelineEntryOrigin.External,
            SourceKey = sourceKey,
            SourceId = sourceId,
        };

    [Fact]
    public async Task Merge_ThrowsDepthExceeded_WhenPageBeyondMaxPage()
    {
        TimelineOptions options = new() { MaxPage = 5 };

        TimelineDepthExceededException ex = await Should.ThrowAsync<TimelineDepthExceededException>(() =>
            TimelineStreamMerger.MergeAsync(
                "User", "u-1", page: 6, pageSize: 10, options,
                sources: [],
                fetchNativeTopAsync: (_, _) => Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([]),
                countNativeAsync: _ => Task.FromResult(0),
                NullLogger.Instance,
                CancellationToken.None));

        ex.RequestedPage.ShouldBe(6);
        ex.MaxPage.ShouldBe(5);
    }

    [Fact]
    public async Task Merge_DedupsExternalEntriesWhenShadowExists()
    {
        // Native side carries a shadow for ("auditing", "a-1"); the source also
        // re-emits "a-1". The shadow must win, dropping the external duplicate.
        TimelineOptions options = new();
        StubSource auditing = new("auditing", [External("auditing", "a-1", 10), External("auditing", "a-2", 5)]);

        TimelineStreamResult result = await TimelineStreamMerger.MergeAsync(
            "User", "u-1", page: 1, pageSize: 50, options,
            sources: [auditing],
            fetchNativeTopAsync: (_, _) => Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([Shadow("auditing", "a-1", 10)]),
            countNativeAsync: _ => Task.FromResult(1),
            NullLogger.Instance,
            CancellationToken.None);

        result.Page.Items.Count.ShouldBe(2); // shadow + a-2 (a-1 deduped)
        result.Page.Items.ShouldContain(e => e.SourceId == "a-1" && e.Body == "shadow-of-a-1");
        result.Page.Items.ShouldContain(e => e.SourceId == "a-2" && e.Body == "auditing-a-2");
    }

    [Fact]
    public async Task Merge_SortsNewestFirst_NativeBeatsExternalOnTies_ThenSourceKeyAsc()
    {
        TimelineOptions options = new();
        TimelineStreamEntry nativeAtT10 = Native(10);
        TimelineStreamEntry externalA_at_T10 = External("a-source", "x", 10);
        TimelineStreamEntry externalB_at_T10 = External("b-source", "x", 10);
        TimelineStreamEntry nativeAtT20 = Native(20);

        StubSource a = new("a-source", [externalA_at_T10]);
        StubSource b = new("b-source", [externalB_at_T10]);

        TimelineStreamResult result = await TimelineStreamMerger.MergeAsync(
            "User", "u-1", page: 1, pageSize: 50, options,
            sources: [a, b],
            fetchNativeTopAsync: (_, _) => Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([nativeAtT10, nativeAtT20]),
            countNativeAsync: _ => Task.FromResult(2),
            NullLogger.Instance,
            CancellationToken.None);

        result.Page.Items[0].Id.ShouldBe(nativeAtT20.Id);     // newest
        result.Page.Items[1].Id.ShouldBe(nativeAtT10.Id);     // tie at T10, native first
        result.Page.Items[2].Id.ShouldBe(externalA_at_T10.Id); // then SourceKey ASC
        result.Page.Items[3].Id.ShouldBe(externalB_at_T10.Id);
    }

    [Fact]
    public async Task Merge_DegradeGracefully_DropsFailingSourceAndReportsIt()
    {
        TimelineOptions options = new() { OnSourceFailure = SourceFailurePolicy.DegradeGracefully };
        FailingSource failing = new("auditing");
        StubSource ok = new("workflow", [External("workflow", "w-1", 5)]);

        TimelineStreamResult result = await TimelineStreamMerger.MergeAsync(
            "User", "u-1", page: 1, pageSize: 50, options,
            sources: [failing, ok],
            fetchNativeTopAsync: (_, _) => Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([]),
            countNativeAsync: _ => Task.FromResult(0),
            NullLogger.Instance,
            CancellationToken.None);

        result.DegradedSources.ShouldBe(["auditing"]);
        result.Page.Items.Count.ShouldBe(1);
        result.Page.Items[0].SourceKey.ShouldBe("workflow");
    }

    [Fact]
    public async Task Merge_ThrowAll_PropagatesSourceFailure()
    {
        TimelineOptions options = new() { OnSourceFailure = SourceFailurePolicy.ThrowAll };
        FailingSource failing = new("auditing");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            TimelineStreamMerger.MergeAsync(
                "User", "u-1", page: 1, pageSize: 50, options,
                sources: [failing],
                fetchNativeTopAsync: (_, _) => Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([]),
                countNativeAsync: _ => Task.FromResult(0),
                NullLogger.Instance,
                CancellationToken.None));
    }

    private sealed class StubSource(string key, IReadOnlyList<TimelineStreamEntry> entries) : ITimelineSource
    {
        public string SourceKey => key;
        public Task<IReadOnlyList<TimelineStreamEntry>> GetEntriesAsync(string _, string __, int ___, CancellationToken ____) =>
            Task.FromResult(entries);
        public Task<TimelineStreamEntry?> GetEntryAsync(string _, string __, string ___, CancellationToken ____) =>
            Task.FromResult<TimelineStreamEntry?>(null);
    }

    private sealed class FailingSource(string key) : ITimelineSource
    {
        public string SourceKey => key;
        public Task<IReadOnlyList<TimelineStreamEntry>> GetEntriesAsync(string _, string __, int ___, CancellationToken ____) =>
            throw new InvalidOperationException("boom");
        public Task<TimelineStreamEntry?> GetEntryAsync(string _, string __, string ___, CancellationToken ____) =>
            Task.FromResult<TimelineStreamEntry?>(null);
    }
}
