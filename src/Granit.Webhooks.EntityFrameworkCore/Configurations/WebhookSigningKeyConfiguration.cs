using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Webhooks.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebhookSigningKey"/>.
/// Table: <c>webhook_signing_keys</c>.
/// </summary>
/// <remarks>
/// FK to <see cref="WebhookSubscription"/> with <see cref="DeleteBehavior.Cascade"/>: signing
/// keys are an integral part of the subscription aggregate and have no meaning without it.
/// </remarks>
internal sealed class WebhookSigningKeyConfiguration : IEntityTypeConfiguration<WebhookSigningKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WebhookSigningKey> builder)
    {
        builder.ToTable(
            GranitWebhooksDbProperties.DbTablePrefix + "signing_keys",
            GranitWebhooksDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SubscriptionId).IsRequired();

        builder.Property(e => e.ProtectedSecret)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ExpiresAt);
        builder.Property(e => e.RevokedAt);
        builder.Property(e => e.LastRotationNotificationAt);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(WebhookSigningKeyStatus.Active);

        // Hot path: rotation scanner (FU-1b) filters by (Status, ExpiresAt, CreatedAt).
        builder.HasIndex(e => new { e.SubscriptionId, e.Status })
            .HasDatabaseName($"ix_{GranitWebhooksDbProperties.DbTablePrefix}signing_keys_subscription_status");

        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName($"ix_{GranitWebhooksDbProperties.DbTablePrefix}signing_keys_expires_at");
    }
}
