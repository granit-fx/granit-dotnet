// =============================================================================
// Tests - IOwnable index convention
// =============================================================================
// Verifies that ApplyGranitConventions auto-creates an index on (TenantId, OwnerId)
// for IOwnable + IMultiTenant entities, or (OwnerId) alone otherwise, with the
// expected database-name pattern. De-duplicates against manual indexes.
// =============================================================================

using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class OwnershipIndexConventionTests
{
    [Fact]
    public void Convention_AddsIndex_OnTenantIdAndOwnerId_ForIOwnableMultiTenantEntity()
    {
        IIndex? index = FindOwnerIndex<OwnableMultiTenantTestEntity>();

        index.ShouldNotBeNull();
        index.Properties.Select(p => p.Name).ShouldBe([nameof(IMultiTenant.TenantId), nameof(IOwnable.OwnerId)]);
        index.GetDatabaseName().ShouldBe("ix_ownable_multi_tenant_test_entities_tenant_owner");
    }

    [Fact]
    public void Convention_AddsIndex_OnOwnerIdAlone_ForIOwnableNonMultiTenantEntity()
    {
        IIndex? index = FindOwnerIndex<OwnableOnlyTestEntity>();

        index.ShouldNotBeNull();
        index.Properties.Select(p => p.Name).ShouldBe([nameof(IOwnable.OwnerId)]);
        index.GetDatabaseName().ShouldBe("ix_ownable_only_test_entities_owner");
    }

    [Fact]
    public void Convention_DoesNotAddIndex_ForNonOwnableEntity()
    {
        IEntityType entityType = GetEntityType<NonOwnableTestEntity>();
        // No IOwnable interface ⇒ convention skips the entity entirely.
        entityType.GetIndexes().ShouldBeEmpty();
    }

    [Fact]
    public void Convention_DoesNotDuplicate_WhenIdenticalManualIndexAlreadyExists()
    {
        IEntityType entityType = GetEntityType<ManuallyIndexedOwnableTestEntity>();

        // Only the manual index should be present — convention de-dupes by checking
        // for an existing index over the same property set.
        var indexes = entityType.GetIndexes()
            .Where(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(IMultiTenant.TenantId), nameof(IOwnable.OwnerId)]))
            .ToList();

        indexes.Count.ShouldBe(1);
        // The manual index name wins — convention did not overwrite.
        indexes[0].GetDatabaseName().ShouldBe("ix_manual_tenant_owner_custom");
    }

    private static IIndex? FindOwnerIndex<TEntity>() where TEntity : class
    {
        IEntityType entityType = GetEntityType<TEntity>();
        return entityType.GetIndexes().FirstOrDefault(idx =>
            idx.Properties.Any(p => p.Name == nameof(IOwnable.OwnerId)));
    }

    private static IEntityType GetEntityType<TEntity>() where TEntity : class
    {
        using OwnershipTestDbContext context = new();
        return context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} not found in model.");
    }
}

#pragma warning disable CA1812 // instantiated by EF Core via reflection

internal sealed class OwnershipTestDbContext : DbContext
{
    public DbSet<OwnableMultiTenantTestEntity> MultiTenantOwned => Set<OwnableMultiTenantTestEntity>();
    public DbSet<OwnableOnlyTestEntity> Owned => Set<OwnableOnlyTestEntity>();
    public DbSet<NonOwnableTestEntity> NonOwnable => Set<NonOwnableTestEntity>();
    public DbSet<ManuallyIndexedOwnableTestEntity> ManuallyIndexed => Set<ManuallyIndexedOwnableTestEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("DataSource=:memory:");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Stable table names so the expected index database names below are deterministic.
        modelBuilder.Entity<OwnableMultiTenantTestEntity>(b =>
        {
            b.ToTable("ownable_multi_tenant_test_entities");
            b.HasKey(e => e.Id);
        });
        modelBuilder.Entity<OwnableOnlyTestEntity>(b =>
        {
            b.ToTable("ownable_only_test_entities");
            b.HasKey(e => e.Id);
        });
        modelBuilder.Entity<NonOwnableTestEntity>(b =>
        {
            b.ToTable("non_ownable_test_entities");
            b.HasKey(e => e.Id);
        });
        modelBuilder.Entity<ManuallyIndexedOwnableTestEntity>(b =>
        {
            b.ToTable("manual_ownable_test_entities");
            b.HasKey(e => e.Id);
            // Pre-existing manual index on the same property set — the convention must
            // detect this and not stack a second index.
            b.HasIndex(nameof(IMultiTenant.TenantId), nameof(IOwnable.OwnerId))
                .HasDatabaseName("ix_manual_tenant_owner_custom");
        });

        modelBuilder.ApplyGranitConventions();
    }
}

internal sealed class OwnableMultiTenantTestEntity : IMultiTenant, IOwnable
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid OwnerId { get; private set; }
}

internal sealed class OwnableOnlyTestEntity : IOwnable
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; private set; }
}

internal sealed class NonOwnableTestEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class ManuallyIndexedOwnableTestEntity : IMultiTenant, IOwnable
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid OwnerId { get; private set; }
}
