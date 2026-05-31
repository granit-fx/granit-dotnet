using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext for the Granit.Webhooks persistence layer.
/// </summary>
/// <remarks>
/// Can be used as a standalone context or integrated into an existing application DbContext
/// by applying <see cref="Configurations.WebhookSubscriptionConfiguration"/> and
/// <see cref="Configurations.WebhookDeliveryAttemptConfiguration"/> in the application's <c>OnModelCreating</c>.
/// </remarks>
internal sealed class WebhooksDbContext(
    DbContextOptions<WebhooksDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Webhook subscriptions.</summary>
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    /// <summary>
    /// Signing keys per subscription. Backs the dual-key delivery and verification model
    /// (FU-1a) — overlap rotation: an old <see cref="WebhookSigningKeyStatus.Retired"/> key
    /// is still accepted while a new <see cref="WebhookSigningKeyStatus.Active"/> key takes
    /// over signing.
    /// </summary>
    public DbSet<WebhookSigningKey> WebhookSigningKeys => Set<WebhookSigningKey>();

    /// <summary>Immutable ISO 27001 audit trail of delivery attempts.</summary>
    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureWebhooksModule();
}
