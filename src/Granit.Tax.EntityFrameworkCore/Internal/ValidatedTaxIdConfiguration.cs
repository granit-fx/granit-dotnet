using Granit.Tax.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Tax.EntityFrameworkCore.Internal;

internal sealed class ValidatedTaxIdConfiguration : IEntityTypeConfiguration<ValidatedTaxId>
{
    public void Configure(EntityTypeBuilder<ValidatedTaxId> builder)
    {
        builder.ToTable(
            GranitTaxDbProperties.DbTablePrefix + "validated_tax_ids",
            GranitTaxDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TaxId).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsValid).IsRequired();
        builder.Property(e => e.Source).IsRequired();
        builder.Property(e => e.ValidatedAt).IsRequired();
        builder.Property(e => e.ExpiresAt);
        builder.Property(e => e.CompanyName).HasMaxLength(500);
        builder.Property(e => e.CompanyAddress).HasMaxLength(1000);
        builder.Property(e => e.RequestIdentifier).HasMaxLength(100);

        builder.HasIndex(e => new { e.TenantId, e.TaxId })
            .HasDatabaseName($"ix_{GranitTaxDbProperties.DbTablePrefix}validated_tax_ids_tenant_taxid");

        builder.HasIndex(e => e.Source)
            .HasFilter($"\"{nameof(ValidatedTaxId.Source)}\" = {(int)TaxIdValidationSource.OfflinePending}")
            .HasDatabaseName($"ix_{GranitTaxDbProperties.DbTablePrefix}validated_tax_ids_pending");
    }
}
