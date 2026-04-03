using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "plans",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.PricingModel).IsRequired();
        builder.Property(e => e.DefaultInterval).IsRequired();
        builder.Property(e => e.TrialDays);
        builder.Property(e => e.SeatLimit);
        builder.Property(e => e.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LifecycleStatus).IsRequired();

        builder.HasMany(e => e.Prices).WithOne().HasForeignKey("PlanId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.PlanFeatureValues).WithOne().HasForeignKey("PlanId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.ExternalMappings).WithOne().HasForeignKey("PlanId").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.LifecycleStatus)
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}plans_lifecycle_status");
    }
}
