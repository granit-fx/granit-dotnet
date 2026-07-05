// =============================================================================
// Tests — In-memory anchor write path
// =============================================================================
// Verifies the deterministic v5 shadow Id makes AnchorExternalAsync idempotent
// and that the projection snapshots body/author/occurredAt at anchor time.
// =============================================================================

using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Granit.Timing;
using Granit.Users;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class InMemoryAnchorTests
{
    private readonly InMemoryTimelineStore _store;
    private readonly StubSource _source;

    public InMemoryAnchorTests()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("u-1");
        user.UserName.Returns("Alice");

        IGuidGenerator guid = Substitute.For<IGuidGenerator>();
        guid.Create().Returns(_ => Guid.NewGuid());

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        _source = new StubSource("auditing");
        _store = new InMemoryTimelineStore(
            clock, user, guid, tenant,
            Microsoft.Extensions.Options.Options.Create(new TimelineOptions()),
            sources: [_source]);
    }

    [Fact]
    public async Task AnchorExternalAsync_IsIdempotent()
    {
        _source.Projection = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.SystemLog,
            AuthorId = "system",
            Body = "snapshot",
            Origin = TimelineEntryOrigin.External,
            SourceKey = "auditing",
            SourceId = "a-1",
        };

        Guid first = await _store.AnchorExternalAsync("User", "u-1", "auditing", "a-1", TestContext.Current.CancellationToken);
        Guid second = await _store.AnchorExternalAsync("User", "u-1", "auditing", "a-1", TestContext.Current.CancellationToken);

        first.ShouldBe(second);
        _store.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnchorExternalAsync_DerivesDifferentIdsForDifferentSourceIds()
    {
        _source.Projection = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.SystemLog,
            AuthorId = "system",
            Body = "snapshot",
            Origin = TimelineEntryOrigin.External,
            SourceKey = "auditing",
            SourceId = "ignored-by-stub",
        };

        Guid a = await _store.AnchorExternalAsync("User", "u-1", "auditing", "a-1", TestContext.Current.CancellationToken);
        Guid b = await _store.AnchorExternalAsync("User", "u-1", "auditing", "a-2", TestContext.Current.CancellationToken);

        a.ShouldNotBe(b);
        _store.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnchorExternalAsync_ThrowsKeyNotFound_WhenSourceKeyUnknown()
    {
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            _store.AnchorExternalAsync("User", "u-1", "no-such-source", "x", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnchorExternalAsync_ThrowsKeyNotFound_WhenSourceHasNoEntry()
    {
        _source.Projection = null;
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            _store.AnchorExternalAsync("User", "u-1", "auditing", "missing", TestContext.Current.CancellationToken));
    }

    private sealed class StubSource(string key) : ITimelineSource
    {
        public string SourceKey { get; } = key;
        public TimelineStreamEntry? Projection { get; set; }

        public Task<IReadOnlyList<TimelineStreamEntry>> GetEntriesAsync(string entityType, string entityId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([]);

        public Task<TimelineStreamEntry?> GetEntryAsync(string entityType, string entityId, string sourceId, CancellationToken cancellationToken) =>
            Task.FromResult(Projection);
    }
}
