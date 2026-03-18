using Granit.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Notifications.EntityFrameworkCore.Configurations;

internal sealed class NotificationSubscriptionConfiguration : IEntityTypeConfiguration<NotificationSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationSubscription> builder)
    {
        builder.ToTable(
            GranitNotificationsDbProperties.DbTablePrefix + "subscriptions",
            GranitNotificationsDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NotificationTypeName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(256);
        builder.Property(x => x.EntityId).HasMaxLength(256);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);

        // Global subscription
        builder.HasIndex(x => new { x.UserId, x.NotificationTypeName, x.TenantId })
            .HasDatabaseName($"ix_{GranitNotificationsDbProperties.DbTablePrefix}subscriptions_global");

        // Entity followers
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.TenantId })
            .HasDatabaseName($"ix_{GranitNotificationsDbProperties.DbTablePrefix}subscriptions_entity");
    }
}
