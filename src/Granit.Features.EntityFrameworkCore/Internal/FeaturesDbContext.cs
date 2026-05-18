using Granit.DataFiltering;
using Granit.Features.EntityFrameworkCore.Entities;
using Granit.Features.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Features.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit feature value overrides.
/// </summary>
/// <remarks>
/// <para>
/// Isolated from the host application's DbContext to avoid coupling.
/// Stores only <see cref="TenantFeatureOverride"/> records — the feature definitions
/// and default values live in code via <c>FeatureDefinitionProvider</c>.
/// </para>
/// <para>
/// Compatible with PostgreSQL (ISO 27001 compliant).
/// </para>
/// </remarks>
internal sealed class FeaturesDbContext(
    DbContextOptions<FeaturesDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Tenant-level feature value overrides.</summary>
    public DbSet<TenantFeatureOverride> FeatureOverrides { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureFeaturesModule();
}
