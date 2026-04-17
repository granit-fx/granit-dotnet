using Granit.Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class MeterDefinitionConfiguration : IEntityTypeConfiguration<MeterDefinition>
{
    public void Configure(EntityTypeBuilder<MeterDefinition> builder)
    {
        builder.ToTable(
            GranitMeteringDbProperties.DbTablePrefix + "meter_definitions",
            GranitMeteringDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.AggregationType).IsRequired();
        builder.Property(e => e.Activated).IsRequired().HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}meter_definitions_tenant_name");
    }
}
