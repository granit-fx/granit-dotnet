using Granit.DataFiltering;
using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Customization.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for entity-customization persistence.
/// Constructor-injects the optional <see cref="ICurrentTenant"/> +
/// <see cref="IDataFilter"/> so <c>ApplyGranitConventions</c> wires the
/// standard tenant filter, soft-delete filter, and audited-entity / lifecycle
/// interceptors.
/// </summary>
internal sealed class CustomizationDbContext(
    DbContextOptions<CustomizationDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Per-tenant Layer 1 customizations (one row per <c>(TenantId, EntityName, LayoutKind)</c>).</summary>
    public DbSet<EntityCustomization> EntityCustomizations => Set<EntityCustomization>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureEntitiesCustomizationModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
