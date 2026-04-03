using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class PlanFeatureValueConfiguration : IEntityTypeConfiguration<PlanFeatureValue>
{
    public void Configure(EntityTypeBuilder<PlanFeatureValue> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "plan_feature_values",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.FeatureName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(500).IsRequired();
    }
}
