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
    public async Task MutateAsync_creates_then_mutates_then_returns_saved_aggregate()
    {
        InMemoryPresenceStore store = new();
        IClock clock = CreateClock();
        var userId = Guid.NewGuid();

        UserPresence saved = await store.MutateAsync(
            userId,
            factory: () => UserPresence.Create(userId, clock),
            mutator: p => p.SetOverride(ManualPresenceStatus.Busy, null, clock),
            CancellationToken.None);

        saved.ManualStatus.ShouldBe(ManualPresenceStatus.Busy);

        UserPresence? roundTrip = await store.GetAsync(userId, CancellationToken.None);
        roundTrip.ShouldNotBeNull();
        roundTrip!.ManualStatus.ShouldBe(ManualPresenceStatus.Busy);
    }

    [Fact]
    public async Task MutateAsync_reuses_existing_aggregate_on_second_call()
    {
        InMemoryPresenceStore store = new();
        IClock clock = CreateClock();
        var userId = Guid.NewGuid();

        UserPresence first = await store.MutateAsync(
            userId,
            () => UserPresence.Create(userId, clock),
            p => p.SetOverride(ManualPresenceStatus.Busy, null, clock),
            CancellationToken.None);

        UserPresence second = await store.MutateAsync(
            userId,
            () => throw new InvalidOperationException("factory should not be invoked when aggregate exists"),
            p => p.SetOverride(ManualPresenceStatus.DoNotDisturb, null, clock),
            CancellationToken.None);

        ReferenceEquals(first, second).ShouldBeTrue();
        second.ManualStatus.ShouldBe(ManualPresenceStatus.DoNotDisturb);
    }

    [Fact]
    public async Task GetManyAsync_omits_missing_entries()
    {
        InMemoryPresenceStore store = new();
        IClock clock = CreateClock();
        var existingId = Guid.NewGuid();
        await store.MutateAsync(
            existingId,
            () => UserPresence.Create(existingId, clock),
            _ => { /* no-op */ },
            CancellationToken.None);

        var missing = Guid.NewGuid();
        IReadOnlyDictionary<Guid, UserPresence> result = await store
            .GetManyAsync([existingId, missing], CancellationToken.None);

        result.Count.ShouldBe(1);
        result.ShouldContainKey(existingId);
        result.ShouldNotContainKey(missing);
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent()
    {
        InMemoryPresenceStore store = new();

        await Should.NotThrowAsync(() => store.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
