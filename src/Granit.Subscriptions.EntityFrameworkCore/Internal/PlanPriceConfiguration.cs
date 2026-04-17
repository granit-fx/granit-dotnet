using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class PlanPriceConfiguration : IEntityTypeConfiguration<PlanPrice>
{
    public void Configure(EntityTypeBuilder<PlanPrice> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "plan_prices",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Interval).IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.ReplacedByPriceId);
        builder.Property(e => e.ReplacedAt);

        builder.HasIndex(e => new { e.ReplacedByPriceId })
            .HasFilter("\"ReplacedByPriceId\" IS NULL")
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}plan_prices_current");

        builder.Ignore(e => e.IsCurrent);
    }
}
