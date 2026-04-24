using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class SubscriptionDiscountConfiguration : IEntityTypeConfiguration<SubscriptionDiscount>
{
    public void Configure(EntityTypeBuilder<SubscriptionDiscount> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "subscription_discounts",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.SubscriptionId).IsRequired();
        builder.Property(e => e.Type).IsRequired();
        // Precision (18, 4) covers Percentage 0–100 with 4 decimals, FixedAmount up to
        // ~99 quadrillion units, and Trial day-count (always integral).
        builder.Property(e => e.Value).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(SubscriptionDiscount.ReasonMaxLength).IsRequired();
        builder.Property(e => e.ExpiresAt);

        builder.HasIndex(e => new { e.SubscriptionId, e.ExpiresAt })
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}subscription_discounts_active");
    }
}
