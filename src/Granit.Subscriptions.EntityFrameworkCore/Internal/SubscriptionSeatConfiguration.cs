using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class SubscriptionSeatConfiguration : IEntityTypeConfiguration<SubscriptionSeat>
{
    public void Configure(EntityTypeBuilder<SubscriptionSeat> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "subscription_seats",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.AssignedAt).IsRequired();

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}seats_user_id");

        builder.HasIndex("SubscriptionId", nameof(SubscriptionSeat.UserId))
            .IsUnique()
            .HasDatabaseName($"ix_{GranitSubscriptionsDbProperties.DbTablePrefix}seats_subscription_user_unique");
    }
}
