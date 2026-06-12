using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Granit.UserSessions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.EntityFrameworkCore.Tests;

public sealed class EfCoreUserSessionRiskStoreTests
{
    private static readonly DateTimeOffset AssessedAt = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DbContextOptions<UserSessionRiskDbContext> _dbOptions =
        new DbContextOptionsBuilder<UserSessionRiskDbContext>()
            .UseInMemoryDatabase($"UserSessionRisk-{Guid.NewGuid()}")
            .Options;

    [Fact]
    public async Task SetThenGet_RoundtripsVerdict()
    {
        EfCoreUserSessionRiskStore store = CreateStore();
        UserSessionRiskVerdict verdict = new(UserSessionRiskLevel.High, ["impossible_travel"], AssessedAt);

        await store.SetAsync("user-1", "s1", verdict, Ct);
        UserSessionRiskVerdict? result = await store.GetAsync("user-1", "s1", Ct);

        result.ShouldNotBeNull();
        result.Level.ShouldBe(UserSessionRiskLevel.High);
        result.Reasons.ShouldBe(["impossible_travel"]);
        result.AssessedAt.ShouldBe(AssessedAt);
    }

    [Fact]
    public async Task SetAsync_Upserts_DoesNotDuplicate()
    {
        EfCoreUserSessionRiskStore store = CreateStore();

        await store.SetAsync("user-1", "s1", new UserSessionRiskVerdict(UserSessionRiskLevel.Low, [], AssessedAt), Ct);
        await store.SetAsync("user-1", "s1", new UserSessionRiskVerdict(UserSessionRiskLevel.High, ["x"], AssessedAt), Ct);

        await using UserSessionRiskDbContext db = new(_dbOptions, GranitDesignTime.CurrentTenant);
        (await db.UserSessionRisks.CountAsync(Ct)).ShouldBe(1);
        (await store.GetAsync("user-1", "s1", Ct))!.Level.ShouldBe(UserSessionRiskLevel.High);
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNull() =>
        (await CreateStore().GetAsync("user-1", "missing", Ct)).ShouldBeNull();

    [Fact]
    public async Task GetManyAsync_ReturnsOnlyRecordedSessionsForUser()
    {
        EfCoreUserSessionRiskStore store = CreateStore();
        await store.SetAsync("user-1", "a", new UserSessionRiskVerdict(UserSessionRiskLevel.Low, [], AssessedAt), Ct);
        await store.SetAsync("user-1", "c", new UserSessionRiskVerdict(UserSessionRiskLevel.Medium, [], AssessedAt), Ct);
        await store.SetAsync("user-2", "a", new UserSessionRiskVerdict(UserSessionRiskLevel.High, [], AssessedAt), Ct);

        IReadOnlyDictionary<string, UserSessionRiskVerdict> result =
            await store.GetManyAsync("user-1", ["a", "b", "c"], Ct);

        result.Keys.ShouldBe(["a", "c"], ignoreOrder: true);
        result["c"].Level.ShouldBe(UserSessionRiskLevel.Medium);
    }

    private EfCoreUserSessionRiskStore CreateStore()
    {
        IGuidGenerator guid = Substitute.For<IGuidGenerator>();
        guid.Create().Returns(_ => Guid.NewGuid());
        return new EfCoreUserSessionRiskStore(new Factory(_dbOptions), guid);
    }

    private sealed class Factory(DbContextOptions<UserSessionRiskDbContext> options)
        : IDbContextFactory<UserSessionRiskDbContext>
    {
        public UserSessionRiskDbContext CreateDbContext() => new(options, GranitDesignTime.CurrentTenant);
    }
}
