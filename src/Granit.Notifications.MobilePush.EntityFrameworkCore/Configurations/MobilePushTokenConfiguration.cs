using Granit.Notifications.EntityFrameworkCore;
using Granit.Notifications.MobilePush.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Notifications.MobilePush.EntityFrameworkCore.Configurations;

internal sealed class MobilePushTokenConfiguration : IEntityTypeConfiguration<MobilePushToken>
{
    public void Configure(EntityTypeBuilder<MobilePushToken> builder)
    {
        builder.ToTable(
            GranitNotificationsDbProperties.DbTablePrefix + "mobile_push_tokens",
            GranitNotificationsDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(256).IsRequired();

        // DeviceToken is encrypted at rest via [Encrypted]. Ciphertext is longer
        // than plaintext (AES + base64 overhead); 4096 comfortably holds an
        // FCM/APNs token (max ~512 bytes plaintext) wrapped as ciphertext.
        builder.Property(x => x.DeviceToken).HasMaxLength(4096).IsRequired();

        // DeviceTokenHash is an HMAC-SHA256 digest (32 bytes → 64 hex chars). It
        // is NOT a secret and NOT a plain hash (peppered HMAC). Indexed with
        // TenantId for exact-match upsert / remove on the encrypted token column.
        builder.Property(x => x.DeviceTokenHash).HasMaxLength(64).IsRequired();

        builder.Property(x => x.Platform).HasMaxLength(16);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);

        // Unique constraint: one device token per tenant — keyed on the lookup
        // hash since the encrypted DeviceToken column is non-deterministic and
        // cannot enforce uniqueness on the plaintext.
        builder.HasIndex(x => new { x.DeviceTokenHash, x.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitNotificationsDbProperties.DbTablePrefix}mobile_push_tokens_device_hash_tenant");

        // Lookup by user + tenant
        builder.HasIndex(x => new { x.UserId, x.TenantId })
            .HasDatabaseName($"ix_{GranitNotificationsDbProperties.DbTablePrefix}mobile_push_tokens_user_tenant");
    }
}
