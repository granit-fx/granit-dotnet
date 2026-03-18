using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Webhooks.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebhookSubscription"/>.
/// Table: <c>webhook_subscriptions</c>.
/// </summary>
internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable(
            GranitWebhooksDbProperties.DbTablePrefix + "subscriptions",
            GranitWebhooksDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TargetUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasMaxLength(200)
            .IsRequired();

        // Protected signing secret — never store plaintext in production.
        builder.Property(e => e.SigningSecret)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.TenantId);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(WebhookSubscriptionStatus.Active);

        builder.Property(e => e.DeactivationReason)
            .HasMaxLength(500);

        builder.Property(e => e.ConsecutiveFailureCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastSuccessAt);

        builder.Property(e => e.SuspendedAt);

        builder.Property(e => e.SuspendedBy)
            .HasMaxLength(450);

        // Audit fields from AuditedEntity / CreationAuditedEntity.
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(450);
        builder.Property(e => e.ModifiedAt);
        builder.Property(e => e.ModifiedBy).HasMaxLength(450);

        // Hot path: fan-out query filters on (EventType, TenantId, Status).
        builder.HasIndex(e => new { e.EventType, e.TenantId, e.Status })
            .HasDatabaseName($"ix_{GranitWebhooksDbProperties.DbTablePrefix}subscriptions_eventtype_tenantid_status");
    }
}
