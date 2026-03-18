using Granit.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Notifications.EntityFrameworkCore.Configurations;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable(
            GranitNotificationsDbProperties.DbTablePrefix + "preferences",
            GranitNotificationsDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NotificationTypeName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ChannelName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.UserId, x.NotificationTypeName, x.ChannelName, x.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitNotificationsDbProperties.DbTablePrefix}preferences_user_type_channel_tenant");
    }
}
