using Granit.Dashboards.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit dashboard entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class DashboardsModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit Dashboards module.</summary>
    public static ModelBuilder ConfigureDashboardsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DashboardConfiguration());
        modelBuilder.ApplyConfiguration(new WidgetInstanceConfiguration());
        return modelBuilder;
    }
}
