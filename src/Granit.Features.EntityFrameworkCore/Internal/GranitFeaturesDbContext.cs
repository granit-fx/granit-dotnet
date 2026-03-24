using Granit.DataFiltering;
using Granit.Features.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
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
internal sealed class GranitFeaturesDbContext(
    DbContextOptions<GranitFeaturesDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Tenant-level feature value overrides.</summary>
    public DbSet<TenantFeatureOverride> FeatureOverrides { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureFeaturesModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
