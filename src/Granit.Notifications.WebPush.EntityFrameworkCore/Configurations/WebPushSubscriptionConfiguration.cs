using Granit.Notifications.EntityFrameworkCore;
using Granit.Notifications.WebPush.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Notifications.WebPush.EntityFrameworkCore.Configurations;

internal sealed class WebPushSubscriptionConfiguration : IEntityTypeConfiguration<WebPushSubscription>
{
    public void Configure(EntityTypeBuilder<WebPushSubscription> builder)
    {
        builder.ToTable(
            GranitNotificationsDbProperties.DbTablePrefix + "web_push_subscriptions",
            GranitNotificationsDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(256).IsRequired();

        // The push endpoint is an opaque URL issued by the browser's push service.
        // It is the natural lookup key (upsert / remove key on it) and is stored in
        // plaintext so it stays queryable; 2048 covers all known push services.
        builder.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();

        // P256dh / Auth are the ECDH key material used to encrypt push payloads.
        // Encrypted at rest via [Encrypted]; ciphertext is longer than the ~88/24
        // char Base64 plaintext (AES + base64 overhead), so size generously.
        builder.Property(x => x.P256dh).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(256).IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(256);

        // Unique constraint: one row per browser endpoint per tenant — a push
        // endpoint is globally unique, so re-subscribing upserts rather than dupes.
        builder.HasIndex(x => new { x.Endpoint, x.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitNotificationsDbProperties.DbTablePrefix}web_push_subscriptions_endpoint_tenant");

        // Lookup by user + tenant (fan-out to all of a user's browsers).
        builder.HasIndex(x => new { x.UserId, x.TenantId })
            .HasDatabaseName($"ix_{GranitNotificationsDbProperties.DbTablePrefix}web_push_subscriptions_user_tenant");
    }
}
