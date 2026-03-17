using Granit.Core.Domain;
using Granit.Persistence.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class ConcurrencyStampInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ConcurrencyStampInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    // ========================================================================
    // Initial stamp on new entity
    // ========================================================================

    [Fact]
    public async Task SaveChangesAsync_NewEntity_SetsInitialConcurrencyStamp()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestConcurrencyEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "New entity",
        };
        context.Entities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
        Guid.TryParse(entity.ConcurrencyStamp, out _).ShouldBeTrue("stamp must be a valid GUID string");
    }

    // ========================================================================
    // Stamp regeneration on modify
    // ========================================================================

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_RegeneratesStamp()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestConcurrencyEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Original",
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        string initialStamp = entity.ConcurrencyStamp;

        // Act — modify the entity
        entity.Name = "Updated";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — stamp must be different
        entity.ConcurrencyStamp.ShouldNotBe(initialStamp);
        entity.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
    }

    // ========================================================================
    // Sync variant
    // ========================================================================

    [Fact]
    public void SaveChanges_Sync_SetsConcurrencyStamp()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestConcurrencyEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Sync test",
        };
        context.Entities.Add(entity);

        // Act
        context.SaveChanges();

        // Assert
        entity.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
    }

    // ========================================================================
    // Non-IConcurrencyAware entities — should be ignored
    // ========================================================================

    [Fact]
    public async Task SaveChangesAsync_NonConcurrencyAwareEntity_IsIgnored()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestPlainEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Label = "Not concurrency-aware",
        };
        context.PlainEntities.Add(entity);

        // Act — should not throw
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.Label.ShouldBe("Not concurrency-aware");
    }

    // ========================================================================
    // Concurrent modification — connected scenario (same DB, two contexts)
    // ========================================================================

    [Fact]
    public async Task SaveChangesAsync_ConcurrentModification_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange — create entity via context1
        var entityId = Guid.NewGuid();
        using TestDbContext context1 = CreateContext();
        TestConcurrencyEntity entity1 = new()
        {
            Id = entityId,
            Name = "Original",
        };
        context1.Entities.Add(entity1);
        await context1.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Load the same entity in context2 (EF tracks OriginalValue automatically)
        using TestDbContext context2 = CreateContext(ensureCreated: false);
        TestConcurrencyEntity entity2 = (await context2.Entities.FindAsync([entityId], TestContext.Current.CancellationToken))!;
        entity2.ShouldNotBeNull();

        // Modify and save via context1 (stamp changes in DB)
        entity1.Name = "Updated by context1";
        await context1.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — modify and save via context2 (still has old stamp) → should throw
        entity2.Name = "Updated by context2";

        // Assert
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => context2.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Disconnected update — OriginalValue pattern for CQRS
    // ========================================================================

    [Fact]
    public async Task SaveChangesAsync_DisconnectedUpdate_WithStaleStamp_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange — create entity and capture its initial stamp
        var entityId = Guid.NewGuid();
        using TestDbContext context1 = CreateContext();
        TestConcurrencyEntity entity = new()
        {
            Id = entityId,
            Name = "Original",
        };
        context1.Entities.Add(entity);
        await context1.SaveChangesAsync(TestContext.Current.CancellationToken);

        string staleStamp = entity.ConcurrencyStamp;

        // Modify and save — stamp rotates
        entity.Name = "Updated once";
        await context1.SaveChangesAsync(TestContext.Current.CancellationToken);
        entity.ConcurrencyStamp.ShouldNotBe(staleStamp, "stamp must have rotated");

        // Simulate a disconnected update: load entity in context2, set stale OriginalValue
        using TestDbContext context2 = CreateContext(ensureCreated: false);
        TestConcurrencyEntity disconnected = (await context2.Entities.FindAsync([entityId], TestContext.Current.CancellationToken))!;

        // Force the OriginalValue to the stale stamp (simulating a frontend sending an old stamp)
        context2.Entry(disconnected).Property(e => e.ConcurrencyStamp).OriginalValue = staleStamp;

        // Act — modify and save with stale stamp
        disconnected.Name = "Disconnected update";

        // Assert — EF Core detects mismatch between OriginalValue and DB value
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => context2.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Test infrastructure
    // ========================================================================

    private TestDbContext CreateContext(bool ensureCreated = true)
    {
        ConcurrencyStampInterceptor interceptor = new();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        var context = new TestDbContext(options);

        if (ensureCreated)
        {
            context.Database.EnsureCreated();
        }

        return context;
    }

    public void Dispose() => _connection.Dispose();

    // --- Test entities ---

    private sealed class TestConcurrencyEntity : Entity, IConcurrencyAware
    {
        public string Name { get; set; } = string.Empty;
        public string ConcurrencyStamp { get; set; } = string.Empty;
    }

    private sealed class TestPlainEntity : Entity
    {
        public string Label { get; set; } = string.Empty;
    }

    // --- Test DbContext ---

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestConcurrencyEntity> Entities => Set<TestConcurrencyEntity>();
        public DbSet<TestPlainEntity> PlainEntities => Set<TestPlainEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestConcurrencyEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
                b.Property(e => e.ConcurrencyStamp).HasMaxLength(36).IsConcurrencyToken();
            });

            modelBuilder.Entity<TestPlainEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }
}
