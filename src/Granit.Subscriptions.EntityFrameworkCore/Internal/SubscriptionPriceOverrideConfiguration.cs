using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class SubscriptionPriceOverrideConfiguration : IEntityTypeConfiguration<SubscriptionPriceOverride>
{
    public void Configure(EntityTypeBuilder<SubscriptionPriceOverride> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "subscription_price_overrides",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.SubscriptionId).IsRequired();
        builder.Property(e => e.PlanPriceId).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.EffectiveUntil);
        builder.Property(e => e.Reason).HasMaxLength(SubscriptionPriceOverride.ReasonMaxLength).IsRequired();

        // Active-window lookup: for a given (subscription, planPrice) the orchestrator
        // probes by EffectiveFrom — the index covers both the subscription scope and
        // the timeline access pattern.
        builder.HasIndex(e => new { e.SubscriptionId, e.PlanPriceId, e.EffectiveFrom })
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}subscription_price_overrides_active");
    }
}
