using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Notifications.EntityFrameworkCore.Configurations;

internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable(
            GranitNotificationsDbProperties.DbTablePrefix + "user_notifications",
            GranitNotificationsDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NotificationTypeName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RecipientUserId).HasMaxLength(256).IsRequired();

        builder.Property(x => x.Data).HasJsonConversion();

        builder.Property(x => x.RelatedEntityType).HasMaxLength(256);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(256);

        // Paginated inbox
        builder.HasIndex(x => new { x.RecipientUserId, x.TenantId, x.State, x.CreatedAt })
            .IsDescending(false, false, false, true)
            .HasDatabaseName($"ix_{GranitNotificationsDbProperties.DbTablePrefix}user_notifications_inbox");

        // Activity feed: per-entity notification history
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId, x.TenantId, x.CreatedAt })
            .IsDescending(false, false, false, true)
            .HasDatabaseName($"ix_{GranitNotificationsDbProperties.DbTablePrefix}user_notifications_entity_feed");
    }
}
