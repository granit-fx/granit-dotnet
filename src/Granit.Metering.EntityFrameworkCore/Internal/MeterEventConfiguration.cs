using Granit.Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class MeterEventConfiguration : IEntityTypeConfiguration<MeterEvent>
{
    public void Configure(EntityTypeBuilder<MeterEvent> builder)
    {
        builder.ToTable(
            GranitMeteringDbProperties.DbTablePrefix + "meter_events",
            GranitMeteringDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MeterDefinitionId).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(18, 6).IsRequired();
        builder.Property(e => e.Timestamp).IsRequired();
        builder.Property(e => e.Metadata).HasMaxLength(4000);
        builder.Property(e => e.DeprecatedAt);
        builder.Property(e => e.DeprecationReason).HasMaxLength(MeterEvent.DeprecationReasonMaxLength);

        // Deduplication index: unique per tenant + idempotency key
        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}meter_events_dedup");

        // Query index: events per meter ordered by ID (for watermark-based aggregation)
        builder.HasIndex(e => new { e.TenantId, e.MeterDefinitionId, e.Id })
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}meter_events_aggregation");
    }
}
