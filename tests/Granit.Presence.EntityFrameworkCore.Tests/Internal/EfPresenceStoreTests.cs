using Granit.Presence.Domain;
using Granit.Presence.EntityFrameworkCore.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Behavioural coverage of <see cref="EfPresenceStore"/> against a real (SQLite in-memory)
/// <see cref="PresenceDbContext"/>, which also exercises the DbContext model and the
/// <c>UserPresence</c> entity configuration.
/// </summary>
public sealed class EfPresenceStoreTests
{
    private static readonly IClock Clock = BuildClock();

    private static IClock BuildClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        return clock;
    }

    [Fact]
    public async Task GetAsync_returns_null_when_no_presence_exists()
    {
        using var factory = TestPresenceDbContextFactory.Create();
        EfPresenceStore store = new(factory);

        UserPresence? result = await store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task MutateAsync_creates_presence_when_absent_and_GetAsync_reads_it_back()
    {
        using var factory = TestPresenceDbContextFactory.Create();
        EfPresenceStore store = new(factory);
        var userId = Guid.NewGuid();

        UserPresence created = await store.MutateAsync(
            userId,
            () => UserPresence.Create(userId, Clock),
            _ => { },
            TestContext.Current.CancellationToken);

        created.UserId.ShouldBe(userId);

        UserPresence? read = await store.GetAsync(userId, TestContext.Current.CancellationToken);
        read.ShouldNotBeNull();
        read.UserId.ShouldBe(userId);
        read.ManualStatus.ShouldBe(ManualPresenceStatus.Available);
    }

    [Fact]
    public async Task MutateAsync_updates_existing_presence()
    {
        using var factory = TestPresenceDbContextFactory.Create();
        EfPresenceStore store = new(factory);
        var userId = Guid.NewGuid();
        var until = new DateTimeOffset(2026, 1, 16, 10, 0, 0, TimeSpan.Zero);

        await store.MutateAsync(
            userId, () => UserPresence.Create(userId, Clock), _ => { }, TestContext.Current.CancellationToken);

        await store.MutateAsync(
            userId,
            () => UserPresence.Create(userId, Clock),
            p => p.SetOverride(ManualPresenceStatus.Busy, until, Clock),
            TestContext.Current.CancellationToken);

        UserPresence? read = await store.GetAsync(userId, TestContext.Current.CancellationToken);
        read.ShouldNotBeNull();
        read.ManualStatus.ShouldBe(ManualPresenceStatus.Busy);
        read.OverrideUntilUtc.ShouldBe(until);
    }

    [Fact]
    public async Task GetManyAsync_returns_empty_dictionary_for_empty_input()
    {
        using var factory = TestPresenceDbContextFactory.Create();
        EfPresenceStore store = new(factory);

        IReadOnlyDictionary<Guid, UserPresence> result =
            await store.GetManyAsync([], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetManyAsync_returns_only_matching_rows()
    {
        using var factory = TestPresenceDbContextFactory.Create();
        EfPresenceStore store = new(factory);
        var present = Guid.NewGuid();
        var absent = Guid.NewGuid();

        await store.MutateAsync(
            present, () => UserPresence.Create(present, Clock), _ => { }, TestContext.Current.CancellationToken);

        IReadOnlyDictionary<Guid, UserPresence> result =
            await store.GetManyAsync([present, absent], TestContext.Current.CancellationToken);

        result.ShouldContainKey(present);
        result.ShouldNotContainKey(absent);
    }

    [Fact]
    public async Task DeleteAsync_hard_deletes_the_presence_row()
    {
        using var factory = TestPresenceDbContextFactory.Create();
        EfPresenceStore store = new(factory);
        var userId = Guid.NewGuid();

        await store.MutateAsync(
            userId, () => UserPresence.Create(userId, Clock), _ => { }, TestContext.Current.CancellationToken);

        await store.DeleteAsync(userId, TestContext.Current.CancellationToken);

        UserPresence? read = await store.GetAsync(userId, TestContext.Current.CancellationToken);
        read.ShouldBeNull();
    }
}
