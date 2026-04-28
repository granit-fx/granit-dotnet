using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for the Granit Dashboards persistence layer.
/// </summary>
/// <remarks>
/// Constructor-injects <see cref="ICurrentTenant"/> and <see cref="IDataFilter"/> so
/// <c>ApplyGranitConventions</c> wires the multi-tenant filter and the soft-delete
/// filter at model build time. Hosts that prefer to integrate dashboards into an
/// existing DbContext should call <see cref="DashboardsModelBuilderExtensions.ConfigureDashboardsModule"/>
/// from their own <c>OnModelCreating</c>.
/// </remarks>
internal sealed class DashboardsDbContext(
    DbContextOptions<DashboardsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null) : DbContext(options)
{
    /// <summary>Tenant-composed dashboards.</summary>
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();

    /// <summary>Widgets pinned on dashboards (owned by the <see cref="Dashboard"/> aggregate).</summary>
    public DbSet<WidgetInstance> WidgetInstances => Set<WidgetInstance>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureDashboardsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
