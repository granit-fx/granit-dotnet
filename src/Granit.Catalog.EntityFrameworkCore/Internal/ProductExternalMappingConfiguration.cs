using Granit.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Catalog.EntityFrameworkCore.Internal;

internal sealed class ProductExternalMappingConfiguration : IEntityTypeConfiguration<ProductExternalMapping>
{
    public void Configure(EntityTypeBuilder<ProductExternalMapping> builder)
    {
        builder.ToTable(
            GranitCatalogDbProperties.DbTablePrefix + "product_external_mappings",
            GranitCatalogDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(256).IsRequired();

        // A given external identifier maps to exactly one product across the catalog
        // (e.g., one Stripe prod_xxx → one Product). Mirrors PlanExternalMapping.
        builder.HasIndex(e => new { e.ProviderName, e.ExternalId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitCatalogDbProperties.DbTablePrefix}product_ext_provider_id");
    }
}
