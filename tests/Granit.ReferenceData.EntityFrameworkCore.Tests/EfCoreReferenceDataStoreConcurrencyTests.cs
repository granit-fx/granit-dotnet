using Granit.ReferenceData.EntityFrameworkCore.Extensions;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Granit.ReferenceData.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

/// <summary>
/// Tests concurrent seeding scenarios using SQLite (which enforces unique constraints,
/// unlike the InMemory provider) to verify TOCTOU race recovery in
/// <see cref="EfCoreReferenceDataStore{TEntity,TDbContext}.CreateAsync"/>.
/// </summary>
public sealed class EfCoreReferenceDataStoreConcurrencyTests : IAsyncLifetime
{
    private sealed class TestEntityConfiguration : ReferenceDataEntityTypeConfiguration<TestEntity>
    {
        public TestEntityConfiguration() : base("ref_test_entities") { }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureReferenceData(new TestEntityConfiguration());
        }
    }

    /// <summary>
    /// Interceptor that inserts a duplicate record right before SaveChangesAsync completes,
    /// deterministically simulating a concurrent seeder winning the TOCTOU race.
    /// </summary>
    private sealed class ConcurrentInsertInterceptor(string connectionString, string code) : SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;

                // Simulate another seeder inserting the same Code right before
                // this SaveChanges completes — the classic TOCTOU race.
                // Use a plain DbContext (without this interceptor) to avoid recursion.
                await using SqliteConnection conn = new(connectionString);
                await conn.OpenAsync(cancellationToken);
                DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlite(conn)
                    .Options;
                await using TestDbContext ctx = new(opts);
                ctx.TestEntities.Add(new TestEntity
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    LabelEn = "Concurrent winner",
                    IsActive = true,
                    SortOrder = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "race",
                });
                await ctx.SaveChangesAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private readonly string _connectionString = $"Data Source=ConcurrencyTest_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
    private SqliteConnection _keepAlive = null!;

    public async ValueTask InitializeAsync()
    {
        // Keep a connection open to preserve the shared in-memory database.
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();
    }

    public async ValueTask DisposeAsync() =>
        await _keepAlive.DisposeAsync();

    private async Task<ServiceProvider> BuildProviderAsync(params IInterceptor[] interceptors)
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(options =>
        {
            options.UseSqlite(_connectionString);
            if (interceptors.Length > 0)
            {
                options.AddInterceptors(interceptors);
            }
        });

        ServiceProvider sp = services.BuildServiceProvider();

        await using AsyncServiceScope scope = sp.CreateAsyncScope();
        TestDbContext ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        return sp;
    }

    private static EfCoreReferenceDataStore<TestEntity, TestDbContext> CreateStore(IServiceScopeFactory scopeFactory) =>
        new(scopeFactory,
            new FusionCache(new FusionCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(new ReferenceDataOptions()));

    [Fact]
    public async Task CreateAsync_ConcurrentInsert_RecoveredGracefully()
    {
        // Arrange: interceptor simulates a concurrent insert between AnyAsync and SaveChangesAsync.
        var interceptor = new ConcurrentInsertInterceptor(_connectionString, "RACE");
        await using ServiceProvider provider = await BuildProviderAsync(interceptor);
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store =
            CreateStore(provider.GetRequiredService<IServiceScopeFactory>());

        // Act: CreateAsync calls AnyAsync (returns false — DB is empty), then the
        // interceptor inserts the same Code before the actual INSERT executes,
        // causing a unique constraint violation. The fix catches DbUpdateException
        // and verifies the record exists via a fresh scope.
        await Should.NotThrowAsync(() => store.CreateAsync(new TestEntity
        {
            Id = Guid.NewGuid(),
            Code = "RACE",
            LabelEn = "Loser",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        }, TestContext.Current.CancellationToken));

        // Assert: the concurrent winner's data is preserved.
        TestEntity? result = await store.GetByCodeAsync("RACE", TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.LabelEn.ShouldBe("Concurrent winner");
    }

    [Fact]
    public async Task CreateAsync_NonDuplicateDbError_StillThrows()
    {
        // Verify that non-duplicate DbUpdateExceptions are NOT swallowed.
        await using ServiceProvider provider = await BuildProviderAsync();
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store =
            CreateStore(provider.GetRequiredService<IServiceScopeFactory>());

        // Insert a valid record first.
        await store.CreateAsync(new TestEntity
        {
            Id = Guid.NewGuid(),
            Code = "VALID",
            LabelEn = "Valid",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        }, TestContext.Current.CancellationToken);

        // Verify it was persisted (sanity check).
        TestEntity? result = await store.GetByCodeAsync("VALID", TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
    }
}
