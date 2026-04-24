using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class PricingTierConfiguration : IEntityTypeConfiguration<PricingTier>
{
    public void Configure(EntityTypeBuilder<PricingTier> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "pricing_tiers",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlanPriceId).IsRequired();
        builder.Property(e => e.SortOrder).IsRequired();
        builder.Property(e => e.UpToQuantity).HasPrecision(18, 4);
        builder.Property(e => e.UnitAmount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.FlatAmount).HasPrecision(18, 4);

        // Sequence integrity at the storage layer: a single PlanPrice cannot have
        // two tiers with the same SortOrder, and the open-ended last tier (UpToQuantity
        // IS NULL) is unique per price by virtue of the SortOrder uniqueness.
        builder.HasIndex(e => new { e.PlanPriceId, e.SortOrder })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}pricing_tiers_sequence");
    }
}
