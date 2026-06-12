using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Granit.UserSessions.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.EntityFrameworkCore.Tests;

public sealed class EfCoreUserSessionRiskStoreTests : IDisposable
{
    private static readonly DateTimeOffset AssessedAt = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // SQLite (not InMemory): a relational provider that actually enforces the unique (UserId, SessionId) index
    // the store's insert-race retry depends on. UseInMemoryDatabase ignores indexes, so it can never trip the
    // DbUpdateException the retry catches — the load-bearing branch would go untested.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<UserSessionRiskDbContext> _dbOptions;

    public EfCoreUserSessionRiskStoreTests()
    {
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<UserSessionRiskDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>()
            .Options;

        using UserSessionRiskDbContext db = new(_dbOptions, GranitDesignTime.CurrentTenant);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

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
    public async Task SetAsync_ConcurrentFirstWrites_ConvergeToOneRow()
    {
        // Two assessments of the same (user, session) racing from a cold start both miss the existing row and
        // both attempt an insert; SQLite enforces the unique index, so one insert trips DbUpdateException and the
        // store's retry re-reads and updates the winner's row. Whatever the interleaving, the invariant holds:
        // exactly one row, no exception escapes. (On InMemory this test was vacuous — no index to trip.)
        EfCoreUserSessionRiskStore store = CreateStore();

        await Task.WhenAll(
            store.SetAsync("user-1", "s1", new UserSessionRiskVerdict(UserSessionRiskLevel.High, ["a"], AssessedAt), Ct),
            store.SetAsync("user-1", "s1", new UserSessionRiskVerdict(UserSessionRiskLevel.High, ["b"], AssessedAt), Ct));

        await using UserSessionRiskDbContext db = new(_dbOptions, GranitDesignTime.CurrentTenant);
        (await db.UserSessionRisks.CountAsync(Ct)).ShouldBe(1);
        (await store.GetAsync("user-1", "s1", Ct))!.Level.ShouldBe(UserSessionRiskLevel.High);
    }

    [Fact]
    public async Task UniqueIndex_RejectsDuplicateUserSessionPair()
    {
        // Proves the constraint the retry relies on is real under the test provider: a second row with the same
        // (UserId, SessionId) but a distinct Id must be rejected.
        await using (UserSessionRiskDbContext db = new(_dbOptions, GranitDesignTime.CurrentTenant))
        {
            db.UserSessionRisks.Add(NewEntity("user-1", "s1"));
            await db.SaveChangesAsync(Ct);
        }

        await using (UserSessionRiskDbContext db = new(_dbOptions, GranitDesignTime.CurrentTenant))
        {
            db.UserSessionRisks.Add(NewEntity("user-1", "s1"));
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
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

    private static UserSessionRiskEntity NewEntity(string userId, string sessionId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SessionId = sessionId,
        Level = UserSessionRiskLevel.Low,
        ReasonsJson = "[]",
        AssessedAt = AssessedAt,
    };

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

    // SQLite has no native DateTimeOffset mapping; convert it (matches the pattern used by every other
    // *.EntityFrameworkCore.Tests project, e.g. Granit.Identity.EntityFrameworkCore.Tests).
    private sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
        : RelationalModelCustomizer(dependencies)
    {
        private static readonly ValueConverter<DateTimeOffset, long> Converter = new(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(Converter);
                    }
                }
            }
        }
    }
}
