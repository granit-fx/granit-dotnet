using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Internal;
using Granit.Presence.Options;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Presence.Tests.Internal;

/// <summary>
/// Behavioural tests for the resource-room tracker. Drives a real <see cref="IFusionCache"/>
/// so the cache-key shape, multi-tab dedup, MAX rule, stale-entry filter and empty-room
/// eviction can all be observed end-to-end inside the test process.
/// </summary>
public sealed class FusionCacheResourcePresenceTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResourceRef SampleResource = new("document", "abc-123");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Lightweight test rig that wraps the tracker + its supporting fakes.</summary>
    private sealed class TestRig
    {
        public required FusionCacheResourcePresenceTracker Tracker { get; init; }
        public required IClock Clock { get; init; }
        public required IFusionCache Cache { get; init; }
    }

    private static TestRig CreateTracker(
        PresenceOptions? options = null,
        ICurrentTenant? currentTenant = null,
        IMeterFactory? meterFactory = null)
    {
        ServiceCollection services = [];
        services.AddFusionCache();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        IFusionCache cache = sp.GetRequiredService<IFusionCache>();

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        IOptionsMonitor<PresenceOptions> optionsMonitor = Substitute.For<IOptionsMonitor<PresenceOptions>>();
        optionsMonitor.CurrentValue.Returns(options ?? new PresenceOptions());

        ICurrentTenant tenant = currentTenant ?? Substitute.For<ICurrentTenant>();

        PresenceMetrics metrics = new(meterFactory ?? sp.GetRequiredService<IMeterFactory>());

        return new TestRig
        {
            Tracker = new FusionCacheResourcePresenceTracker(cache, clock, tenant, optionsMonitor, metrics),
            Clock = clock,
            Cache = cache,
        };
    }

    [Fact]
    public async Task JoinAsync_creates_room_with_single_entry_when_user_first_joins()
    {
        TestRig rig = CreateTracker();
        var userId = Guid.NewGuid();

        ResourceRoom room = await rig.Tracker.JoinAsync(SampleResource, userId, metadata: "{}", Ct);

        room.Resource.ShouldBe(SampleResource);
        room.Participants.Count.ShouldBe(1);
        room.Participants[0].UserId.ShouldBe(userId);
        room.Participants[0].Metadata.ShouldBe("{}");
        room.Participants[0].LastSeenUtc.ShouldBe(Now);
    }

    [Fact]
    public async Task JoinAsync_merges_multiple_distinct_users_into_same_room()
    {
        TestRig rig = CreateTracker();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await rig.Tracker.JoinAsync(SampleResource, alice, cancellationToken: Ct);
        ResourceRoom room = await rig.Tracker.JoinAsync(SampleResource, bob, cancellationToken: Ct);

        room.Participants.Count.ShouldBe(2);
        room.Participants.Select(p => p.UserId).ShouldBe([alice, bob], ignoreOrder: true);
    }

    [Fact]
    public async Task JoinAsync_dedup_multi_tab_keeps_single_entry_per_user_with_max_timestamp_and_last_metadata()
    {
        TestRig rig = CreateTracker();
        var userId = Guid.NewGuid();

        // First tab joins at Now.
        await rig.Tracker.JoinAsync(SampleResource, userId, metadata: "tab-1", Ct);

        // Second tab joins 5 seconds later with new metadata.
        DateTimeOffset later = Now.AddSeconds(5);
        rig.Clock.Now.Returns(later);
        ResourceRoom room = await rig.Tracker.JoinAsync(SampleResource, userId, metadata: "tab-2", Ct);

        room.Participants.Count.ShouldBe(1);
        room.Participants[0].UserId.ShouldBe(userId);
        room.Participants[0].LastSeenUtc.ShouldBe(later);
        room.Participants[0].Metadata.ShouldBe("tab-2");
    }

    [Fact]
    public async Task JoinAsync_applies_max_rule_when_existing_timestamp_is_greater_than_now()
    {
        // Repro: a backplane echo arrives that contains a newer LastSeenUtc than the local clock.
        TestRig rig = CreateTracker();
        var userId = Guid.NewGuid();

        // Pre-seed the cache directly with a forward-dated entry.
        DateTimeOffset future = Now.AddSeconds(30);
        await rig.Cache.SetAsync(
            $"granit.presence.room:global:{SampleResource.Kind}:{SampleResource.Id}",
            new List<ResourcePresenceEntry>
            {
                new(userId, future, "forward-dated"),
            },
            TimeSpan.FromMinutes(2),
            token: Ct);

        rig.Clock.Now.Returns(Now);
        ResourceRoom room = await rig.Tracker.JoinAsync(SampleResource, userId, metadata: "new", Ct);

        room.Participants[0].LastSeenUtc.ShouldBe(future, "MAX rule: keep the larger timestamp");
        room.Participants[0].Metadata.ShouldBe("new", "metadata: last write wins");
    }

    [Fact]
    public async Task JoinAsync_throws_when_metadata_exceeds_512_bytes()
    {
        TestRig rig = CreateTracker();
        string oversize = new('x', 513);

        await Should.ThrowAsync<ArgumentException>(() =>
            rig.Tracker.JoinAsync(SampleResource, Guid.NewGuid(), oversize, Ct));
    }

    [Fact]
    public async Task JoinAsync_accepts_metadata_at_exact_limit()
    {
        TestRig rig = CreateTracker();
        string atLimit = new('x', 512);

        ResourceRoom room = await rig.Tracker.JoinAsync(SampleResource, Guid.NewGuid(), atLimit, Ct);

        room.Participants[0].Metadata!.Length.ShouldBe(512);
    }

    [Fact]
    public async Task JoinAsync_throws_when_kind_violates_regex()
    {
        TestRig rig = CreateTracker();
        var bad = new ResourceRef("Document", "abc"); // uppercase forbidden

        await Should.ThrowAsync<ArgumentException>(() =>
            rig.Tracker.JoinAsync(bad, Guid.NewGuid(), cancellationToken: Ct));
    }

    [Fact]
    public async Task JoinAsync_throws_when_id_exceeds_256_chars()
    {
        TestRig rig = CreateTracker();
        var bad = new ResourceRef("document", new string('x', 257));

        await Should.ThrowAsync<ArgumentException>(() =>
            rig.Tracker.JoinAsync(bad, Guid.NewGuid(), cancellationToken: Ct));
    }

    [Fact]
    public async Task JoinAsync_throws_when_user_id_is_empty()
    {
        TestRig rig = CreateTracker();

        await Should.ThrowAsync<ArgumentException>(() =>
            rig.Tracker.JoinAsync(SampleResource, Guid.Empty, cancellationToken: Ct));
    }

    [Fact]
    public async Task GetAsync_returns_empty_room_when_never_joined()
    {
        TestRig rig = CreateTracker();

        ResourceRoom room = await rig.Tracker.GetAsync(SampleResource, Ct);

        room.Participants.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_filters_stale_entries_at_read_time()
    {
        PresenceOptions opts = new() { OfflineThreshold = TimeSpan.FromMinutes(1) };
        TestRig rig = CreateTracker(opts);
        var stale = Guid.NewGuid();
        var fresh = Guid.NewGuid();

        // Stale entry joined 5 minutes ago.
        rig.Clock.Now.Returns(Now.AddMinutes(-5));
        await rig.Tracker.JoinAsync(SampleResource, stale, cancellationToken: Ct);

        // Fresh join at Now.
        rig.Clock.Now.Returns(Now);
        await rig.Tracker.JoinAsync(SampleResource, fresh, cancellationToken: Ct);

        ResourceRoom room = await rig.Tracker.GetAsync(SampleResource, Ct);

        room.Participants.Select(p => p.UserId).ShouldBe([fresh]);
    }

    [Fact]
    public async Task LeaveAsync_removes_only_the_specified_user()
    {
        TestRig rig = CreateTracker();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await rig.Tracker.JoinAsync(SampleResource, alice, cancellationToken: Ct);
        await rig.Tracker.JoinAsync(SampleResource, bob, cancellationToken: Ct);

        await rig.Tracker.LeaveAsync(SampleResource, alice, Ct);

        ResourceRoom room = await rig.Tracker.GetAsync(SampleResource, Ct);
        room.Participants.Select(p => p.UserId).ShouldBe([bob]);
    }

    [Fact]
    public async Task LeaveAsync_evicts_cache_entry_entirely_when_room_becomes_empty()
    {
        TestRig rig = CreateTracker();
        var userId = Guid.NewGuid();

        await rig.Tracker.JoinAsync(SampleResource, userId, cancellationToken: Ct);
        await rig.Tracker.LeaveAsync(SampleResource, userId, Ct);

        // Direct cache probe — the entry must be gone, not present as an empty list.
        string key = $"granit.presence.room:global:{SampleResource.Kind}:{SampleResource.Id}";
        List<ResourcePresenceEntry>? raw = await rig.Cache.GetOrDefaultAsync<List<ResourcePresenceEntry>?>(
            key, defaultValue: null, token: Ct);
        raw.ShouldBeNull();
    }

    [Fact]
    public async Task LeaveAsync_is_noop_when_user_is_absent()
    {
        TestRig rig = CreateTracker();
        var alice = Guid.NewGuid();

        await rig.Tracker.JoinAsync(SampleResource, alice, cancellationToken: Ct);
        await rig.Tracker.LeaveAsync(SampleResource, Guid.NewGuid(), Ct); // unrelated user

        ResourceRoom room = await rig.Tracker.GetAsync(SampleResource, Ct);
        room.Participants.Select(p => p.UserId).ShouldBe([alice]);
    }

    [Fact]
    public async Task Cache_key_is_scoped_per_tenant()
    {
        // Tenant A
        ICurrentTenant tenantA = Substitute.For<ICurrentTenant>();
        tenantA.IsAvailable.Returns(true);
        tenantA.Id.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        TestRig rigA = CreateTracker(currentTenant: tenantA);

        // Tenant B sharing the same FusionCache instance
        ICurrentTenant tenantB = Substitute.For<ICurrentTenant>();
        tenantB.IsAvailable.Returns(true);
        tenantB.Id.Returns(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        IOptionsMonitor<PresenceOptions> opts = Substitute.For<IOptionsMonitor<PresenceOptions>>();
        opts.CurrentValue.Returns(new PresenceOptions());
        ServiceCollection bServices = [];
        bServices.AddMetrics();
        IMeterFactory bMeterFactory = bServices.BuildServiceProvider().GetRequiredService<IMeterFactory>();
        FusionCacheResourcePresenceTracker trackerB = new(
            rigA.Cache, clock, tenantB, opts, new PresenceMetrics(bMeterFactory));

        var alice = Guid.NewGuid();
        await rigA.Tracker.JoinAsync(SampleResource, alice, cancellationToken: Ct);

        // From tenant B's view the room must be empty (isolation).
        ResourceRoom roomB = await trackerB.GetAsync(SampleResource, Ct);
        roomB.Participants.ShouldBeEmpty();
    }
}
