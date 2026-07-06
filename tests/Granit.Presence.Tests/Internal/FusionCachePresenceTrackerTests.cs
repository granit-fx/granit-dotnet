using Granit.Presence.Domain;
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
/// Behavioural tests for the user-heartbeat tracker. Drives a real <see cref="IFusionCache"/>
/// so the cache-key shape, the idle clamp, the multi-tab MAX(LastActivity) merge rule and the
/// get/get-many/remove round-trips are all observed end-to-end inside the test process.
/// </summary>
public sealed class FusionCachePresenceTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FusionCachePresenceTracker CreateTracker(PresenceOptions? options = null)
    {
        ServiceCollection services = [];
        services.AddFusionCache();
        ServiceProvider sp = services.BuildServiceProvider();
        IFusionCache cache = sp.GetRequiredService<IFusionCache>();

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        IOptionsMonitor<PresenceOptions> optionsMonitor = Substitute.For<IOptionsMonitor<PresenceOptions>>();
        optionsMonitor.CurrentValue.Returns(options ?? new PresenceOptions());

        return new FusionCachePresenceTracker(cache, clock, optionsMonitor);
    }

    [Fact]
    public async Task RecordPollAsync_FirstPoll_DerivesActivityFromNowMinusIdle()
    {
        FusionCachePresenceTracker tracker = CreateTracker();
        var userId = Guid.NewGuid();

        PresenceHeartbeat merged = await tracker.RecordPollAsync(userId, TimeSpan.FromSeconds(10), Ct);

        merged.LastPollUtc.ShouldBe(Now);
        merged.LastActivityUtc.ShouldBe(Now - TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RecordPollAsync_NegativeIdle_Throws()
    {
        FusionCachePresenceTracker tracker = CreateTracker();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            tracker.RecordPollAsync(Guid.NewGuid(), TimeSpan.FromSeconds(-1), Ct));
    }

    [Fact]
    public async Task RecordPollAsync_IdleBeyondTwiceOfflineThreshold_IsClamped()
    {
        // Default OfflineThreshold = 90s, so the clamp ceiling is 180s. An 10-minute idle must
        // be treated as 180s, i.e. activity = Now - 180s (not Now - 600s).
        FusionCachePresenceTracker tracker = CreateTracker();

        PresenceHeartbeat merged = await tracker.RecordPollAsync(Guid.NewGuid(), TimeSpan.FromMinutes(10), Ct);

        merged.LastActivityUtc.ShouldBe(Now - TimeSpan.FromSeconds(180));
    }

    [Fact]
    public async Task RecordPollAsync_SecondPollWithOlderActivity_KeepsMostRecentActivity()
    {
        // Multi-tab dedup: an active tab (small idle) followed by a stale tab (large idle) must not
        // regress LastActivityUtc — the MAX rule keeps the more recent activity instant.
        FusionCachePresenceTracker tracker = CreateTracker();
        var userId = Guid.NewGuid();

        await tracker.RecordPollAsync(userId, TimeSpan.FromSeconds(5), Ct);   // activity = Now - 5s
        PresenceHeartbeat merged = await tracker.RecordPollAsync(userId, TimeSpan.FromSeconds(60), Ct); // computed = Now - 60s

        merged.LastActivityUtc.ShouldBe(Now - TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordPollAsync_PersistsHeartbeatRetrievableByGet()
    {
        FusionCachePresenceTracker tracker = CreateTracker();
        var userId = Guid.NewGuid();

        PresenceHeartbeat recorded = await tracker.RecordPollAsync(userId, TimeSpan.FromSeconds(3), Ct);

        PresenceHeartbeat? fetched = await tracker.GetAsync(userId, Ct);
        fetched.ShouldBe(recorded);
    }

    [Fact]
    public async Task GetAsync_UnknownUser_ReturnsNull()
    {
        FusionCachePresenceTracker tracker = CreateTracker();

        (await tracker.GetAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetManyAsync_ReturnsOnlyPresentEntries()
    {
        FusionCachePresenceTracker tracker = CreateTracker();
        var present = Guid.NewGuid();
        var absent = Guid.NewGuid();
        await tracker.RecordPollAsync(present, TimeSpan.FromSeconds(1), Ct);

        IReadOnlyDictionary<Guid, PresenceHeartbeat> result =
            await tracker.GetManyAsync([present, absent], Ct);

        result.Count.ShouldBe(1);
        result.ShouldContainKey(present);
        result.ShouldNotContainKey(absent);
    }

    [Fact]
    public async Task GetManyAsync_NullUserIds_Throws()
    {
        FusionCachePresenceTracker tracker = CreateTracker();

        await Should.ThrowAsync<ArgumentNullException>(() => tracker.GetManyAsync(null!, Ct));
    }

    [Fact]
    public async Task RemoveAsync_EvictsHeartbeat()
    {
        FusionCachePresenceTracker tracker = CreateTracker();
        var userId = Guid.NewGuid();
        await tracker.RecordPollAsync(userId, TimeSpan.FromSeconds(1), Ct);

        await tracker.RemoveAsync(userId, Ct);

        (await tracker.GetAsync(userId, Ct)).ShouldBeNull();
    }
}
