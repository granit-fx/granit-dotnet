using Granit.Presence.Abstractions;
using Granit.Presence.Domain;
using Granit.Presence.Internal;
using Granit.Presence.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Internal;

public sealed class PresenceQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);

    private static PresenceQueryService CreateService(
        UserPresence? presence = null,
        PresenceHeartbeat? heartbeat = null,
        PresenceOptions? options = null)
    {
        IPresenceStore store = Substitute.For<IPresenceStore>();
        store.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(presence);

        IPresenceTracker tracker = Substitute.For<IPresenceTracker>();
        tracker.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(heartbeat);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        IOptionsMonitor<PresenceOptions> optionsMonitor = Substitute.For<IOptionsMonitor<PresenceOptions>>();
        optionsMonitor.CurrentValue.Returns(options ?? new PresenceOptions());

        return new PresenceQueryService(store, tracker, clock, optionsMonitor);
    }

    [Fact]
    public async Task GetAsync_returns_offline_when_no_override_and_no_heartbeat()
    {
        PresenceQueryService service = CreateService();

        PresenceSnapshot snapshot = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.Offline);
        snapshot.ManualOverride.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_returns_online_when_recent_heartbeat_and_recent_activity()
    {
        var heartbeat = new PresenceHeartbeat(Now.AddSeconds(-10), Now.AddSeconds(-30));
        PresenceQueryService service = CreateService(heartbeat: heartbeat);

        PresenceSnapshot snapshot = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.Online);
    }

    [Fact]
    public async Task GetAsync_returns_away_when_poll_recent_but_activity_old()
    {
        var heartbeat = new PresenceHeartbeat(Now.AddSeconds(-10), Now.AddMinutes(-10));
        PresenceQueryService service = CreateService(heartbeat: heartbeat);

        PresenceSnapshot snapshot = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.Away);
    }

    [Fact]
    public async Task GetAsync_returns_offline_when_poll_stale()
    {
        var heartbeat = new PresenceHeartbeat(Now.AddMinutes(-5), Now.AddSeconds(-30));
        PresenceQueryService service = CreateService(heartbeat: heartbeat);

        PresenceSnapshot snapshot = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.Offline);
    }

    [Fact]
    public async Task GetAsync_returns_dnd_when_override_dnd_regardless_of_connectivity()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var presence = UserPresence.Create(Guid.NewGuid(), clock);
        presence.SetOverride(ManualPresenceStatus.DoNotDisturb, null, clock);

        var heartbeat = new PresenceHeartbeat(Now.AddSeconds(-10), Now.AddSeconds(-10));
        PresenceQueryService service = CreateService(presence, heartbeat);

        PresenceSnapshot snapshot = await service.GetAsync(presence.UserId, CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.DoNotDisturb);
        snapshot.ManualOverride.ShouldBe(ManualPresenceStatus.DoNotDisturb);
    }

    [Fact]
    public async Task GetAsync_returns_offline_when_override_appear_offline()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var presence = UserPresence.Create(Guid.NewGuid(), clock);
        presence.SetOverride(ManualPresenceStatus.AppearOffline, null, clock);

        var heartbeat = new PresenceHeartbeat(Now.AddSeconds(-10), Now.AddSeconds(-10));
        PresenceQueryService service = CreateService(presence, heartbeat);

        PresenceSnapshot snapshot = await service.GetAsync(presence.UserId, CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.Offline);
        snapshot.ManualOverride.ShouldBe(ManualPresenceStatus.AppearOffline);
    }

    [Fact]
    public async Task GetAsync_treats_expired_override_as_cleared()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var presence = UserPresence.Create(Guid.NewGuid(), clock);
        presence.SetOverride(ManualPresenceStatus.Busy, Now.AddMinutes(-1), clock);

        var heartbeat = new PresenceHeartbeat(Now.AddSeconds(-10), Now.AddSeconds(-10));
        PresenceQueryService service = CreateService(presence, heartbeat);

        PresenceSnapshot snapshot = await service.GetAsync(presence.UserId, CancellationToken.None);

        snapshot.EffectiveStatus.ShouldBe(PresenceStatus.Online);
        snapshot.ManualOverride.ShouldBeNull();
    }

    [Fact]
    public async Task GetManyAsync_issues_one_batch_call_per_dependency()
    {
        IPresenceStore store = Substitute.For<IPresenceStore>();
        store.GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, UserPresence>());

        IPresenceTracker tracker = Substitute.For<IPresenceTracker>();
        tracker.GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PresenceHeartbeat>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        IOptionsMonitor<PresenceOptions> options = Substitute.For<IOptionsMonitor<PresenceOptions>>();
        options.CurrentValue.Returns(new PresenceOptions());

        var service = new PresenceQueryService(store, tracker, clock, options);
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        await service.GetManyAsync(ids, CancellationToken.None);

        await store.Received(1).GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        await tracker.Received(1).GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }
}
