using Granit.Guids;
using Granit.Identity.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class EfCoreIdentitySecurityStateStoreProfileTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // SQLite (not InMemory): enforces the unique (UserId, Kind, Value) index the upsert-race retry relies on.
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RecordThenGet_RoundtripsObservations()
    {
        EfCoreIdentitySecurityStateStore store = CreateStore();

        await store.RecordBehavioralObservationAsync("user-1", "BE", "windows", "50,4", T0, Ct);
        UserBehavioralProfile profile = await store.GetBehavioralProfileAsync("user-1", Ct);

        profile.Observations.Count.ShouldBe(3);
        BehavioralObservation country = profile.Observations
            .Single(o => o.Kind == BehavioralObservationKind.Country);
        country.Value.ShouldBe("BE");
        country.Count.ShouldBe(1);
        country.FirstSeenAt.ShouldBe(T0);
    }

    [Fact]
    public async Task Record_Twice_IncrementsCount_OneRowPerValue()
    {
        EfCoreIdentitySecurityStateStore store = CreateStore();

        await store.RecordBehavioralObservationAsync("user-1", "BE", null, null, T0, Ct);
        await store.RecordBehavioralObservationAsync("user-1", "BE", null, null, T0.AddDays(1), Ct);

        await using IdentityDbContext db = _factory.CreateDbContext();
        (await db.UserBehavioralProfiles.CountAsync(Ct)).ShouldBe(1);

        BehavioralObservation be = (await store.GetBehavioralProfileAsync("user-1", Ct)).Observations.Single();
        be.Count.ShouldBe(2);
        be.FirstSeenAt.ShouldBe(T0);
        be.LastSeenAt.ShouldBe(T0.AddDays(1));
    }

    [Fact]
    public async Task RecordConcurrentFirstWrites_ConvergeToOneRowCountedTwice()
    {
        // Two observations of the same (user, country) racing from a cold start both miss the existing row and
        // both insert; SQLite trips the unique index on one, and the store's retry re-reads and increments the
        // winner's row. Invariant: exactly one row, count 2, no exception escapes.
        EfCoreIdentitySecurityStateStore store = CreateStore();

        await Task.WhenAll(
            store.RecordBehavioralObservationAsync("user-1", "BE", null, null, T0, Ct),
            store.RecordBehavioralObservationAsync("user-1", "BE", null, null, T0, Ct));

        await using IdentityDbContext db = _factory.CreateDbContext();
        (await db.UserBehavioralProfiles.CountAsync(Ct)).ShouldBe(1);
        (await store.GetBehavioralProfileAsync("user-1", Ct)).Observations.Single().Count.ShouldBe(2);
    }

    [Fact]
    public async Task UniqueIndex_RejectsDuplicateUserKindValueTriple()
    {
        await using (IdentityDbContext db = _factory.CreateDbContext())
        {
            db.UserBehavioralProfiles.Add(NewEntity("user-1", "BE"));
            await db.SaveChangesAsync(Ct);
        }

        await using (IdentityDbContext db = _factory.CreateDbContext())
        {
            db.UserBehavioralProfiles.Add(NewEntity("user-1", "BE"));
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task Get_UnknownUser_ReturnsEmpty() =>
        (await CreateStore().GetBehavioralProfileAsync("nobody", Ct)).ShouldBe(UserBehavioralProfile.Empty);

    private static UserBehavioralProfileEntity NewEntity(string userId, string value) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = BehavioralObservationKind.Country,
        Value = value,
        Count = 1,
        FirstSeenAt = T0,
        LastSeenAt = T0,
    };

    private EfCoreIdentitySecurityStateStore CreateStore()
    {
        IGuidGenerator guid = Substitute.For<IGuidGenerator>();
        guid.Create().Returns(_ => Guid.NewGuid());
        return new EfCoreIdentitySecurityStateStore(_factory, guid);
    }
}
