using Granit.Presence.Domain;
using Granit.Presence.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Internal;

public sealed class InMemoryPresenceStoreTests
{
    private static IClock CreateClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero));
        return clock;
    }

    [Fact]
    public async Task GetAsync_returns_null_when_user_absent()
    {
        InMemoryPresenceStore store = new();

        UserPresence? result = await store.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertAsync_then_GetAsync_round_trips()
    {
        InMemoryPresenceStore store = new();
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.Busy, null, CreateClock());

        await store.UpsertAsync(presence, CancellationToken.None);
        UserPresence? roundTrip = await store.GetAsync(presence.UserId, CancellationToken.None);

        roundTrip.ShouldNotBeNull();
        roundTrip!.ManualStatus.ShouldBe(ManualPresenceStatus.Busy);
    }

    [Fact]
    public async Task GetManyAsync_omits_missing_entries()
    {
        InMemoryPresenceStore store = new();
        var existing = UserPresence.Create(Guid.NewGuid(), CreateClock());
        await store.UpsertAsync(existing, CancellationToken.None);

        var missing = Guid.NewGuid();
        IReadOnlyDictionary<Guid, UserPresence> result = await store
            .GetManyAsync([existing.UserId, missing], CancellationToken.None);

        result.Count.ShouldBe(1);
        result.ShouldContainKey(existing.UserId);
        result.ShouldNotContainKey(missing);
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent()
    {
        InMemoryPresenceStore store = new();

        await Should.NotThrowAsync(() => store.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
