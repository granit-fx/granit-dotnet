using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit.Taxonomy.
/// </summary>
/// <remarks>
/// Compatible with SQL Server and PostgreSQL. Aggregates and configurations are wired
/// by <see cref="TaxonomyModelBuilderExtensions.ConfigureTaxonomyModule"/>.
/// </remarks>
internal sealed class TaxonomyDbContext(
    DbContextOptions<TaxonomyDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Tenant-scoped tags. Uniqueness enforced on <c>(TenantId, Scope, Name)</c>.</summary>
    public DbSet<Tag> Tags { get; set; } = null!;

    /// <summary>
    /// Polymorphic tag assignments — links tags to target aggregates via
    /// <c>(TargetType, TargetId)</c>. Uniqueness enforced on
    /// <c>(TenantId, TagId, TargetType, TargetId)</c>.
    /// </summary>
    public DbSet<TagAssignment> TagAssignments { get; set; } = null!;

    /// <summary>Hierarchical categories — single-assignment classification (ADR-054 / T4.1).</summary>
    public DbSet<Category> Categories { get; set; } = null!;

    /// <summary>
    /// Polymorphic category assignments — links a single <see cref="Category"/> to a
    /// target aggregate. Uniqueness enforced on <c>(TenantId, TargetType, TargetId)</c>.
    /// </summary>
    public DbSet<CategoryAssignment> CategoryAssignments { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureTaxonomyModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
