using Granit.Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class AggregationWatermarkConfiguration : IEntityTypeConfiguration<AggregationWatermark>
{
    public void Configure(EntityTypeBuilder<AggregationWatermark> builder)
    {
        builder.ToTable(
            GranitMeteringDbProperties.DbTablePrefix + "aggregation_watermarks",
            GranitMeteringDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MeterDefinitionId).IsRequired();
        builder.Property(e => e.LastProcessedEventId).IsRequired();
        builder.Property(e => e.LastProcessedAt);

        builder.HasIndex(e => new { e.TenantId, e.MeterDefinitionId })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}watermarks_tenant_meter");
    }
}
