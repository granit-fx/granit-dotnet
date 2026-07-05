using Granit.Domain;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class VersioningInterceptorTests
{
    private static readonly Guid FixedGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private int _guidCallCount;

    public VersioningInterceptorTests()
    {
        // Each call returns a distinct GUID so we can distinguish multiple VersionId assignments
        _guidGenerator.Create().Returns(_ =>
        {
            int n = ++_guidCallCount;
            return new Guid($"12345678-1234-1234-1234-{n:D12}");
        });
    }

    // ========================================================================
    // VersionId assignment
    // ========================================================================

    [Fact]
    public async Task SaveChanges_WhenVersionIdIsEmpty_ShouldAssignNewVersionId()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Patient v1",
        };
        context.Entities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.VersionId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task SaveChanges_WhenVersionIdIsSet_ShouldNotOverwrite()
    {
        // Arrange
        var existingVersionId = Guid.NewGuid();
        await using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Patient v2",
            VersionId = existingVersionId,
        };
        context.Entities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.VersionId.ShouldBe(existingVersionId);
    }

    // ========================================================================
    // Version assignment
    // ========================================================================

    [Fact]
    public async Task SaveChanges_FirstVersion_ShouldSetVersionTo1()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "First version",
        };
        context.Entities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.Version.ShouldBe(1);
    }

    [Fact]
    public async Task SaveChanges_SecondVersion_ShouldSetVersionTo2()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        await using TestDbContext context = CreateContext();

        // Add first version
        TestVersionedEntity v1 = new()
        {
            Id = Guid.NewGuid(),
            Name = "v1",
            VersionId = businessId,
        };
        context.Entities.Add(v1);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Add second version with same VersionId
        TestVersionedEntity v2 = new()
        {
            Id = Guid.NewGuid(),
            Name = "v2",
            VersionId = businessId,
        };
        context.Entities.Add(v2);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        v1.Version.ShouldBe(1);
        v2.Version.ShouldBe(2);
    }

    [Fact]
    public async Task SaveChanges_MultipleAddsForSameVersionId_ShouldIncrementSequentially()
    {
        // Arrange — two entities added in the same SaveChanges batch
        var businessId = Guid.NewGuid();
        await using TestDbContext context = CreateContext();

        TestVersionedEntity v1 = new()
        {
            Id = Guid.NewGuid(),
            Name = "Batch v1",
            VersionId = businessId,
        };
        TestVersionedEntity v2 = new()
        {
            Id = Guid.NewGuid(),
            Name = "Batch v2",
            VersionId = businessId,
        };
        context.Entities.Add(v1);
        context.Entities.Add(v2);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — versions assigned sequentially within the batch
        int[] versions = [v1.Version, v2.Version];
        Array.Sort(versions);
        versions.ShouldBe([1, 2]);
    }

    // ========================================================================
    // Modified entities — should NOT be touched
    // ========================================================================

    [Fact]
    public async Task SaveChanges_WhenModified_ShouldNotChangeVersion()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            VersionId = Guid.NewGuid(),
            Version = 1,
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — modify the entity (in-place update)
        entity.Name = "Updated";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — version unchanged
        entity.Version.ShouldBe(1);
    }

    // ========================================================================
    // Non-IVersioned entities — should be ignored
    // ========================================================================

    [Fact]
    public async Task SaveChanges_NonVersionedEntity_ShouldBeIgnored()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestPlainEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Label = "Not versioned",
        };
        context.PlainEntities.Add(entity);

        // Act — should not throw
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.Label.ShouldBe("Not versioned");
    }

    // ========================================================================
    // Sync variant
    // ========================================================================

    [Fact]
    public void SaveChanges_Sync_ShouldAssignVersioning()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Sync test",
        };
        context.Entities.Add(entity);

        // Act
        context.SaveChanges();

        // Assert
        entity.VersionId.ShouldNotBe(Guid.Empty);
        entity.Version.ShouldBe(1);
    }

    // ========================================================================
    // Test infrastructure
    // ========================================================================

    private TestDbContext CreateContext()
    {
        VersioningInterceptor interceptor = new(_guidGenerator);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    // --- Test entities ---

    private sealed class TestVersionedEntity : Entity, IVersioned
    {
        public string Name { get; set; } = string.Empty;
        public Guid VersionId { get; set; }
        public int Version { get; set; }
    }

    private sealed class TestPlainEntity : Entity
    {
        public string Label { get; set; } = string.Empty;
    }

    // --- Test DbContext ---

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestVersionedEntity> Entities => Set<TestVersionedEntity>();
        public DbSet<TestPlainEntity> PlainEntities => Set<TestPlainEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestVersionedEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TestPlainEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }
}
