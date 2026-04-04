using Granit.Tax.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Tax.EntityFrameworkCore.Internal;

internal sealed class TaxRateOverrideConfiguration : IEntityTypeConfiguration<TaxRateOverride>
{
    public void Configure(EntityTypeBuilder<TaxRateOverride> builder)
    {
        builder.ToTable(
            GranitTaxDbProperties.DbTablePrefix + "rate_overrides",
            GranitTaxDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(e => e.StandardRate).HasPrecision(6, 4).IsRequired();
        builder.Property(e => e.ReducedRate).HasPrecision(6, 4);
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.EffectiveTo);

        builder.HasIndex(e => new { e.TenantId, e.CountryCode, e.EffectiveFrom })
            .HasDatabaseName($"ix_{GranitTaxDbProperties.DbTablePrefix}rate_overrides_lookup");
    }
}
