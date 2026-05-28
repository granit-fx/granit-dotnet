using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// Common shape exposed by every Webhooks DbContext flavour so stores can dispatch reads
/// and writes regardless of whether the storage layout is <c>Shared</c> (one context) or
/// <c>Segregated</c> (host context + tenant context).
/// </summary>
/// <remarks>
/// Per ADR-063 the two concrete implementations are:
/// <list type="bullet">
///   <item><see cref="WebhooksHostDbContext"/> — host-pinned. Serves both <c>Shared</c> mode (single context for all rows, row-level filter) and the host portion of <c>Segregated</c> mode (host rows only).</item>
///   <item><see cref="WebhooksTenantDbContext"/> — tenant-isolated. Used only under <c>Segregated</c> mode for tenant rows.</item>
/// </list>
/// </remarks>
internal interface IWebhooksDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Webhook subscriptions in the current scope.</summary>
    DbSet<WebhookSubscription> WebhookSubscriptions { get; }

    /// <summary>Signing keys for subscriptions in the current scope.</summary>
    DbSet<WebhookSigningKey> WebhookSigningKeys { get; }

    /// <summary>Delivery attempts for subscriptions in the current scope.</summary>
    DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
