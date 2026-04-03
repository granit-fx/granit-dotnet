using Granit.Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class UsageAggregateConfiguration : IEntityTypeConfiguration<UsageAggregate>
{
    public void Configure(EntityTypeBuilder<UsageAggregate> builder)
    {
        builder.ToTable(
            GranitMeteringDbProperties.DbTablePrefix + "usage_aggregates",
            GranitMeteringDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MeterDefinitionId).IsRequired();
        builder.Property(e => e.Period).IsRequired();
        builder.Property(e => e.PeriodStart).IsRequired();
        builder.Property(e => e.PeriodEnd).IsRequired();
        builder.Property(e => e.AggregatedValue).HasPrecision(18, 6).IsRequired();
        builder.Property(e => e.EventCount).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.MeterDefinitionId, e.Period, e.PeriodStart })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}usage_aggregates_lookup");
    }
}
