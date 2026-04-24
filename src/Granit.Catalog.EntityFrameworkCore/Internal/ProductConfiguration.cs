using System.Text.Json;
using Granit.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Catalog.EntityFrameworkCore.Internal;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    private static readonly ValueConverter<Dictionary<string, string>, string> MetadataConverter =
        new(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => string.IsNullOrEmpty(v)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default)
                  ?? new Dictionary<string, string>(StringComparer.Ordinal));

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            GranitCatalogDbProperties.DbTablePrefix + "products",
            GranitCatalogDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Sku).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LifecycleStatus).IsRequired();

        builder.Property(e => e.Metadata)
            .HasConversion(MetadataConverter)
            .HasMaxLength(4000);

        builder.HasMany(e => e.ExternalMappings)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);

        // Sku is unique within the Host catalog (MVP scope: Product is Host-owned).
        // When the e-commerce phase introduces multi-tenant scoping (ADR 032),
        // this index will need to widen to (TenantId, Sku).
        builder.HasIndex(e => e.Sku)
            .IsUnique()
            .HasDatabaseName($"uq_{GranitCatalogDbProperties.DbTablePrefix}products_sku");

        // Hot path filter: list Published products for downstream consumers.
        builder.HasIndex(e => e.LifecycleStatus)
            .HasDatabaseName($"ix_{GranitCatalogDbProperties.DbTablePrefix}products_lifecycle_status");
    }
}
