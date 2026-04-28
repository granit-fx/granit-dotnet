using System.Text.Json;
using Granit.Dashboards.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Dashboards.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core fluent-API configuration for <see cref="WidgetInstance"/> rows.
/// Table: <c>dashboard_widget_instances</c>.
/// </summary>
internal sealed class WidgetInstanceConfiguration : IEntityTypeConfiguration<WidgetInstance>
{
    public void Configure(EntityTypeBuilder<WidgetInstance> builder)
    {
        builder.ToTable(
            GranitDashboardsDbProperties.DbTablePrefix + "widget_instances",
            GranitDashboardsDbProperties.DbSchema);

        builder.HasKey(w => w.Id);

        builder.Property(w => w.DashboardId).IsRequired();

        builder.Property(w => w.WidgetType)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(w => w.Position).IsRequired();
        builder.Property(w => w.Width).IsRequired();
        builder.Property(w => w.Height).IsRequired();

        builder.Property(w => w.MetricName).HasMaxLength(200);
        builder.Property(w => w.QueryName).HasMaxLength(200);

        // ConfigJson is unbounded — widget configurations vary widely (chart series,
        // pivot dimensions, table columns). Stored as text; per-kind validators
        // (story B3 #1384) ensure it stays parseable on write.
        builder.Property(w => w.ConfigJson).IsRequired();

        builder.Property(w => w.TitleLocalizationKey)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(w => w.RequiredPermission).HasMaxLength(200);

        // Per-instance overrides (P3.2) — persisted as a JSON string. JSON keeps the
        // schema additive when new override fields ship; same trade-off as ConfigJson
        // and intentionally not OwnsOne(...).ToJson() since the override record carries
        // a nullable list of thresholds which round-trips more reliably as a single
        // serialised payload.
        ValueConverter<WidgetInstanceConfig?, string?> overridesConverter = new(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<WidgetInstanceConfig>(v, JsonSerializerOptions.Default));

        ValueComparer<WidgetInstanceConfig?> overridesComparer = new(
            (l, r) => Equals(l, r),
            v => v == null ? 0 : v.GetHashCode(),
            v => v);

        builder.Property(w => w.Overrides)
            .HasColumnName("overrides_json")
            .HasConversion(overridesConverter, overridesComparer);

        // Index supports the typical "list widgets for this dashboard, ordered by position" query.
        builder.HasIndex(w => new { w.DashboardId, w.Position })
            .HasDatabaseName("ix_dashboard_widget_instances_dashboard_position");
    }
}
