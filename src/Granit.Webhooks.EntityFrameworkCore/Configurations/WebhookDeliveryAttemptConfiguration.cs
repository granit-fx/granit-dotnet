using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Webhooks.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebhookDeliveryAttempt"/>.
/// Table: <c>webhook_delivery_attempts</c>.
/// </summary>
/// <remarks>
/// ISO 27001 compliance: this table is INSERT-only. No cascade deletes are configured from
/// <c>webhook_subscriptions</c> — delivery records must be retained for 3 years
/// even after the subscription is deactivated.
/// </remarks>
internal sealed class WebhookDeliveryAttemptConfiguration : IEntityTypeConfiguration<WebhookDeliveryAttempt>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WebhookDeliveryAttempt> builder)
    {
        builder.ToTable(
            GranitWebhooksDbProperties.DbTablePrefix + "delivery_attempts",
            GranitWebhooksDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DeliveryId)
            .IsRequired();

        builder.Property(e => e.SubscriptionId)
            .IsRequired();

        builder.Property(e => e.TenantId);

        builder.Property(e => e.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.TargetUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(e => e.HttpStatusCode);

        // SHA-256 hex = 64 chars.
        builder.Property(e => e.PayloadHash)
            .HasMaxLength(64)
            .IsRequired();

        // PostgreSQL: timestamptz stored in UTC.
        builder.Property(e => e.OccurredAt)
            .IsRequired();

        builder.Property(e => e.DurationMs)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.IsSuccess)
            .IsRequired();

        // Optional: stored only when WebhooksOptions.StorePayload is enabled.
        builder.Property(e => e.Payload)
            .HasColumnType("text");

        // Delivery lookup by subscription (e.g., history view).
        builder.HasIndex(e => new { e.SubscriptionId, e.OccurredAt })
            .HasDatabaseName($"ix_{GranitWebhooksDbProperties.DbTablePrefix}delivery_attempts_subscriptionid_occurredat");

        // GDPR: enables bulk export and erasure by tenant.
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt })
            .HasDatabaseName($"ix_{GranitWebhooksDbProperties.DbTablePrefix}delivery_attempts_tenantid_occurredat");

        // Unique index on DeliveryId for deduplication.
        builder.HasIndex(e => e.DeliveryId)
            .IsUnique()
            .HasDatabaseName($"uq_{GranitWebhooksDbProperties.DbTablePrefix}delivery_attempts_deliveryid");
    }
}
