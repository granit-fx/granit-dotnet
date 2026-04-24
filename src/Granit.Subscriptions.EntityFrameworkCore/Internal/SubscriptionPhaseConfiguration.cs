using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class SubscriptionPhaseConfiguration : IEntityTypeConfiguration<SubscriptionPhase>
{
    public void Configure(EntityTypeBuilder<SubscriptionPhase> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "subscription_phases",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.SubscriptionId).IsRequired();
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.EndDate);

        // PlanId is a SingleValueObject<Guid> — declared as scalar so EF Core does not
        // try to materialise it as a navigation. Converter is applied by ApplyGranitConventions.
        builder.Property(e => e.PlanId).IsRequired();

        builder.Property(e => e.OverridePriceId);
        builder.Property(e => e.DiscountPercent).HasPrecision(5, 2);

        // One overlap-free timeline per subscription — the per-(subscription, start)
        // index is small but enables efficient GetActivePhase point-lookups.
        builder.HasIndex(e => new { e.SubscriptionId, e.StartDate })
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}subscription_phases_timeline");
    }
}
