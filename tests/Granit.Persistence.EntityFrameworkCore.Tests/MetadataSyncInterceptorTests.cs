// =============================================================================
// Tests - MetadataSyncInterceptor
// =============================================================================
// Verifies JSON-bag → shadow-property promotion on save: mapped keys move to
// their shadow column and leave the JSON, unmapped keys stay, and the mapping
// lookup is cached per entity type. SQLite in-memory (relational) so shadow
// columns and the round-trip are real.
// =============================================================================

using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class MetadataSyncInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly IMetadataMappingRegistry _registry;

    public MetadataSyncInterceptorTests()
    {
        _connection.Open();
        _registry = Substitute.For<IMetadataMappingRegistry>();
        _registry.GetMappedPropertyNames(typeof(MetadataEntity)).Returns(["Priority"]);
    }

    public void Dispose() => _connection.Dispose();

    private MetadataDbContext CreateContext()
    {
        DbContextOptions<MetadataDbContext> options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new MetadataSyncInterceptor(_registry))
            .Options;
        MetadataDbContext context = new(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task SaveChangesAsync_MappedKey_PromotedToShadowPropertyAndRemovedFromJson()
    {
        await using MetadataDbContext context = CreateContext();
        MetadataEntity entity = new()
        {
            Id = Guid.NewGuid(),
            MetadataJson = """{"Priority":"High","Color":"Red"}""",
        };
        context.Entities.Add(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.Entry(entity).Property("Priority").CurrentValue.ShouldBe("High");
        entity.MetadataJson.ShouldBe("""{"Color":"Red"}""");
    }

    [Fact]
    public async Task SaveChangesAsync_ShadowColumn_RoundtripsThroughTheDatabase()
    {
        var id = Guid.NewGuid();
        await using (MetadataDbContext context = CreateContext())
        {
            context.Entities.Add(new MetadataEntity
            {
                Id = id,
                MetadataJson = """{"Priority":"Urgent"}""",
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (MetadataDbContext context = CreateContext())
        {
            string? stored = await context.Entities
                .Where(e => e.Id == id)
                .Select(e => EF.Property<string>(e, "Priority"))
                .SingleAsync(TestContext.Current.CancellationToken);
            stored.ShouldBe("Urgent");
        }
    }

    [Fact]
    public async Task SaveChangesAsync_AllKeysMapped_JsonBecomesNull()
    {
        await using MetadataDbContext context = CreateContext();
        MetadataEntity entity = new()
        {
            Id = Guid.NewGuid(),
            MetadataJson = """{"Priority":"Low"}""",
        };
        context.Entities.Add(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.MetadataJson.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_OnlyUnmappedKeys_JsonUntouchedAndShadowStaysNull()
    {
        await using MetadataDbContext context = CreateContext();
        MetadataEntity entity = new()
        {
            Id = Guid.NewGuid(),
            MetadataJson = """{"Color":"Blue"}""",
        };
        context.Entities.Add(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.MetadataJson.ShouldBe("""{"Color":"Blue"}""");
        context.Entry(entity).Property("Priority").CurrentValue.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_NullOrWhitespaceJson_IsNoOp()
    {
        await using MetadataDbContext context = CreateContext();
        MetadataEntity entity = new() { Id = Guid.NewGuid(), MetadataJson = null };
        context.Entities.Add(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.MetadataJson.ShouldBeNull();
        context.Entry(entity).Property("Priority").CurrentValue.ShouldBeNull();
    }

    [Fact]
    public void SaveChanges_SyncPath_PromotesLikeTheAsyncPath()
    {
        using MetadataDbContext context = CreateContext();
        MetadataEntity entity = new()
        {
            Id = Guid.NewGuid(),
            MetadataJson = """{"Priority":"Sync","Color":"Green"}""",
        };
        context.Entities.Add(entity);

        context.SaveChanges();

        context.Entry(entity).Property("Priority").CurrentValue.ShouldBe("Sync");
        entity.MetadataJson.ShouldBe("""{"Color":"Green"}""");
    }

    [Fact]
    public async Task SaveChangesAsync_MappingLookup_IsCachedPerEntityType()
    {
        await using MetadataDbContext context = CreateContext();
        context.Entities.Add(new MetadataEntity { Id = Guid.NewGuid(), MetadataJson = """{"Priority":"A"}""" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.Entities.Add(new MetadataEntity { Id = Guid.NewGuid(), MetadataJson = """{"Priority":"B"}""" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _registry.Received(1).GetMappedPropertyNames(typeof(MetadataEntity));
    }

    private sealed class MetadataEntity : Entity, IHasMetadata
    {
        public string? MetadataJson { get; set; }
    }

    private sealed class MetadataDbContext(DbContextOptions<MetadataDbContext> options) : DbContext(options)
    {
        public DbSet<MetadataEntity> Entities => Set<MetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<MetadataEntity>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property<string?>("Priority");
            });
    }
}
