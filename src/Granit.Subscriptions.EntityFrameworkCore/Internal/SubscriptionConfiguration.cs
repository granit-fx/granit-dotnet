using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "subscriptions",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // PlanId is a SingleValueObject<Guid> — must be declared as a scalar property
        // to prevent EF Core from discovering it as a navigation/entity type.
        // The value converter is applied automatically by ApplyGranitConventions.
        builder.Property(e => e.PlanId).IsRequired();

        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.CurrentPeriodStart).IsRequired();
        builder.Property(e => e.CurrentPeriodEnd).IsRequired();
        builder.Property(e => e.BillingCycleAnchor).IsRequired();
        builder.Property(e => e.TrialEndsAt);
        builder.Property(e => e.CancelAtPeriodEnd).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CancelledAt);
        builder.Property(e => e.CancellationReason).HasMaxLength(500);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.DunningAttempt).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.PlanPriceId);

        builder.HasMany(e => e.Seats).WithOne().HasForeignKey("SubscriptionId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.ExternalMappings).WithOne().HasForeignKey("SubscriptionId").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}subscriptions_tenant_status");

        builder.HasIndex(e => e.TrialEndsAt)
            .HasFilter($"\"Status\" = {(int)SubscriptionStatus.Trial}")
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}subscriptions_trial_ends_at");

        builder.HasIndex(e => e.CurrentPeriodEnd)
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}subscriptions_period_end");
    }
}
