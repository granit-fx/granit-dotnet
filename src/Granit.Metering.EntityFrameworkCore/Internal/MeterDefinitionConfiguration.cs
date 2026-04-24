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
        builder.Property(e => e.DistinctProperty).HasMaxLength(200);
        builder.Property(e => e.LifecycleStatus).IsRequired();
        builder.HasIndex(e => e.LifecycleStatus)
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}meter_definitions_lifecycle");

        // Soft reference to Granit.Catalog.Product — no SQL FK across modules.
        // Indexed for reverse lookups (find all meters for a given product).
        builder.Property(e => e.ProductId);
        builder.HasIndex(e => e.ProductId)
            .HasFilter("\"ProductId\" IS NOT NULL")
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}meter_definitions_product");

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitMeteringDbProperties.DbTablePrefix}meter_definitions_tenant_name");
    }
}
