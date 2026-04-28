using Granit.Dashboards.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Dashboards.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core fluent-API configuration for the <see cref="Dashboard"/> aggregate.
/// Table: <c>dashboard_dashboards</c>.
/// </summary>
internal sealed class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> builder)
    {
        builder.ToTable(
            GranitDashboardsDbProperties.DbTablePrefix + "dashboards",
            GranitDashboardsDbProperties.DbSchema);

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.SourceDefinitionName)
            .HasMaxLength(200);

        builder.Property(d => d.SourceDefinitionVersion)
            .HasMaxLength(50);

        builder.Property(d => d.IsSystem).IsRequired();
        builder.Property(d => d.LayoutColumns).IsRequired();
        builder.Property(d => d.LayoutRowHeight).IsRequired();
        builder.Property(d => d.TenantId);

        // Aggregate child collection — cascade delete keeps widgets tied to their dashboard.
        builder.HasMany(d => d.Widgets)
            .WithOne()
            .HasForeignKey(w => w.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backing field for the read-only navigation property.
        builder.Metadata
            .FindNavigation(nameof(Dashboard.Widgets))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Composite indexes optimised for the admin grid (filter by tenant + status / category).
        builder.HasIndex(d => new { d.TenantId, d.Status })
            .HasDatabaseName("ix_dashboard_dashboards_tenant_status");

        builder.HasIndex(d => new { d.TenantId, d.Category })
            .HasDatabaseName("ix_dashboard_dashboards_tenant_category");
    }
}
