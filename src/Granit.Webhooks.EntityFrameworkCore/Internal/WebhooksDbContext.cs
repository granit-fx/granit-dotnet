using Granit.Core.DataFiltering;
using Granit.Core.MultiTenancy;
using Granit.Persistence.Extensions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext for the Granit.Webhooks persistence layer.
/// </summary>
/// <remarks>
/// Can be used as a standalone context or integrated into an existing application DbContext
/// by applying <see cref="WebhookSubscriptionConfiguration"/> and
/// <see cref="WebhookDeliveryAttemptConfiguration"/> in the application's <c>OnModelCreating</c>.
/// </remarks>
internal sealed class WebhooksDbContext(
    DbContextOptions<WebhooksDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null) : DbContext(options)
{
    /// <summary>Webhook subscriptions.</summary>
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    /// <summary>Immutable ISO 27001 audit trail of delivery attempts.</summary>
    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureWebhooksModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
