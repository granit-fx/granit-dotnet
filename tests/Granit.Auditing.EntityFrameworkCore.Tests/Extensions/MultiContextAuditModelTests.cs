// =============================================================================
// MultiContextAuditModelTests - single-DDL-owner rule for shared audit tables
// =============================================================================
// Verifies the ConfigureAuditingModule(excludeFromMigrations) contract:
//   - Two host context types can map the audit entities over the SAME database
//     (one owning the DDL, one excluded) and both write audit rows successfully.
//   - The excluded context emits NO CreateTable operations for the three audit
//     tables from its migrations model, while the owning context emits all three.
//   - The relational metadata reflects the exclusion flag per audit entity type.
// =============================================================================
// Joins the "AuditingDbProperties" collection: table-name assertions depend on
// GranitAuditingDbProperties.DbTablePrefix, which other classes in that
// collection mutate — running serialized avoids the shared-static race.
// =============================================================================

using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Extensions;

[Collection("AuditingDbProperties")]
public sealed class MultiContextAuditModelTests
{
    private static string[] AuditTableNames =>
    [
        GranitAuditingDbProperties.DbTablePrefix + "log_entries",
        GranitAuditingDbProperties.DbTablePrefix + "entity_changes",
        GranitAuditingDbProperties.DbTablePrefix + "property_changes",
    ];

    private static readonly Type[] AuditEntityTypes =
    [
        typeof(AuditEntry),
        typeof(AuditEntityChange),
        typeof(AuditPropertyChange),
    ];

    // -------------------------------------------------------------------------
    // Shared tables — both contexts write audit rows into the same database
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OwningAndExcludedContexts_BothWriteAuditRows_IntoSharedTables()
    {
        // Arrange — one shared Sqlite database; the owning context creates the full
        // schema, the excluded context only creates its own business table (its audit
        // tables are excluded from DDL, so CreateTables must not collide with the
        // already-existing audit tables).
        await using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<OwningAuditHostContext> owningOptions =
            new DbContextOptionsBuilder<OwningAuditHostContext>().UseSqlite(connection).Options;
        DbContextOptions<SecondaryAuditHostContext> secondaryOptions =
            new DbContextOptionsBuilder<SecondaryAuditHostContext>().UseSqlite(connection).Options;

        await using (OwningAuditHostContext owning = new(owningOptions))
        {
            await owning.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await using (SecondaryAuditHostContext secondary = new(secondaryOptions))
        {
            // Creates only the tables the secondary model still owns (its business
            // table) — throws "table already exists" if the audit exclusion leaks.
            secondary.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        // Act — write one audit entry through each context type.
        var owningEntryId = Guid.NewGuid();
        await using (OwningAuditHostContext owning = new(owningOptions))
        {
            owning.Add(CreateEntry(owningEntryId, "from-owning"));
            owning.Owners.Add(new OwningBusinessEntity { Id = 1, Label = "owner" });
            await owning.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var secondaryEntryId = Guid.NewGuid();
        await using (SecondaryAuditHostContext secondary = new(secondaryOptions))
        {
            secondary.Add(CreateEntry(secondaryEntryId, "from-secondary"));
            secondary.Secondaries.Add(new SecondaryBusinessEntity { Id = 1, Label = "secondary" });
            await secondary.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert — both rows live in the same shared audit table, visible from both models.
        await using (OwningAuditHostContext owning = new(owningOptions))
        {
            List<AuditEntry> entries = await owning.Set<AuditEntry>().AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken);
            entries.Count.ShouldBe(2);
            entries.ShouldContain(e => e.Id == owningEntryId && e.UserId == "from-owning");
            entries.ShouldContain(e => e.Id == secondaryEntryId && e.UserId == "from-secondary");
        }

        await using (SecondaryAuditHostContext secondary = new(secondaryOptions))
        {
            (await secondary.Set<AuditEntry>().AsNoTracking()
                .CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        }
    }

    // -------------------------------------------------------------------------
    // Migration operations — only the owning context emits the audit-table DDL
    // -------------------------------------------------------------------------

    [Fact]
    public void ExcludedContext_MigrationOperations_ContainNoAuditTableDdl()
    {
        // Arrange & Act
        List<string> created = GetCreateTableNames<SecondaryAuditHostContext>(
            options => new SecondaryAuditHostContext(options));

        // Assert — no audit-table DDL, but the context's own business table is there.
        created.ShouldNotContain(AuditTableNames[0]);
        created.ShouldNotContain(AuditTableNames[1]);
        created.ShouldNotContain(AuditTableNames[2]);
        created.ShouldContain("secondary_business_entities");
    }

    [Fact]
    public void OwningContext_MigrationOperations_ContainAllThreeAuditTables()
    {
        // Arrange & Act
        List<string> created = GetCreateTableNames<OwningAuditHostContext>(
            options => new OwningAuditHostContext(options));

        // Assert
        created.ShouldContain(AuditTableNames[0]);
        created.ShouldContain(AuditTableNames[1]);
        created.ShouldContain(AuditTableNames[2]);
        created.ShouldContain("owning_business_entities");
    }

    // -------------------------------------------------------------------------
    // Relational metadata — exclusion flag per audit entity type
    // -------------------------------------------------------------------------

    [Fact]
    public void ExcludedContext_AuditEntities_AreTableExcludedFromMigrations()
    {
        using SecondaryAuditHostContext context = new(BuildOptions<SecondaryAuditHostContext>());

        // The exclusion flag is design-time-only configuration — the read-optimized
        // runtime model drops it, so it must be read from the design-time model.
        IModel designTimeModel = context.GetService<IDesignTimeModel>().Model;

        foreach (Type entityType in AuditEntityTypes)
        {
            IEntityType metadata = designTimeModel.FindEntityType(entityType).ShouldNotBeNull();
            metadata.IsTableExcludedFromMigrations().ShouldBeTrue(
                $"{entityType.Name} must be excluded from the secondary context's migrations");
        }
    }

    [Fact]
    public void OwningContext_AuditEntities_AreNotExcludedFromMigrations()
    {
        using OwningAuditHostContext context = new(BuildOptions<OwningAuditHostContext>());

        IModel designTimeModel = context.GetService<IDesignTimeModel>().Model;

        foreach (Type entityType in AuditEntityTypes)
        {
            IEntityType metadata = designTimeModel.FindEntityType(entityType).ShouldNotBeNull();
            metadata.IsTableExcludedFromMigrations().ShouldBeFalse(
                $"{entityType.Name} must be owned by the owning context's migrations");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DbContextOptions<TContext> BuildOptions<TContext>()
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

    private static List<string> GetCreateTableNames<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
    {
        using TContext context = factory(BuildOptions<TContext>());

        // Diff from an empty model to the design-time model — the exact operation set
        // a first migration would emit for this context.
        IReadOnlyList<MigrationOperation> operations = context
            .GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        return [.. operations.OfType<CreateTableOperation>().Select(o => o.Name)];
    }

    private static AuditEntry CreateEntry(Guid id, string userId) => new()
    {
        Id = id,
        Timestamp = new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero),
        UserId = userId,
        Category = AuditCategory.DataMutation,
        CreatedAt = new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero),
        CreatedBy = userId,
        EntityChanges =
        [
            new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditEntryId = id,
                EntityType = "SharedEntity",
                EntityId = "1",
                ChangeType = AuditChangeType.Created,
            },
        ],
    };

    // -------------------------------------------------------------------------
    // Test contexts — one owning the audit DDL, one mapping without DDL
    // -------------------------------------------------------------------------

    private sealed class OwningAuditHostContext(DbContextOptions<OwningAuditHostContext> options)
        : DbContext(options)
    {
        public DbSet<OwningBusinessEntity> Owners => Set<OwningBusinessEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OwningBusinessEntity>().ToTable("owning_business_entities");
            modelBuilder.ConfigureAuditingModule();
        }
    }

    private sealed class SecondaryAuditHostContext(DbContextOptions<SecondaryAuditHostContext> options)
        : DbContext(options)
    {
        public DbSet<SecondaryBusinessEntity> Secondaries => Set<SecondaryBusinessEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SecondaryBusinessEntity>().ToTable("secondary_business_entities");
            modelBuilder.ConfigureAuditingModule(excludeFromMigrations: true);
        }
    }

    private sealed class OwningBusinessEntity
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private sealed class SecondaryBusinessEntity
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
