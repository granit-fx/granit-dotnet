using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// Central dispatch helper that hands every Webhooks store the right
/// <see cref="IWebhooksDbContext"/> for a given scope, independently of the configured
/// <see cref="DualScopeStorageMode"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063 design decisions for Epic #2377 V1, dispatch is inline (no shared base
/// class) and uses fetch-then-dispatch for writes that need to find the target subscription
/// first. The host-from-tenant fan-out fallback (a tenant event must reach host SIEM
/// forwarders) is preserved under <see cref="DualScopeStorageMode.Segregated"/> via
/// <see cref="OpenAllAsync"/>.
/// </para>
/// <para>
/// <see cref="WebhooksHostDbContext"/> serves both modes: under <c>Shared</c> it is the
/// single context holding every subscription with the row-level filter active; under
/// <c>Segregated</c> it holds only host subscriptions and the tenant factory points at
/// the companion isolated context.
/// </para>
/// </remarks>
internal sealed class WebhooksContextResolver(
    DualScopeStorageMode storageMode,
    IDbContextFactory<WebhooksHostDbContext> hostFactory,
    IDbContextFactory<WebhooksTenantDbContext>? tenantFactory = null)
{
    /// <summary>The active storage mode declared at registration.</summary>
    public DualScopeStorageMode StorageMode { get; } = storageMode;

    /// <summary>
    /// Opens the appropriate context for an operation whose <c>TenantId</c> is known
    /// up-front (e.g. an incoming write whose scope is set by the caller).
    /// </summary>
    public async Task<IWebhooksDbContext> OpenForScopeAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return StorageMode switch
        {
            DualScopeStorageMode.Shared
                => await hostFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false),
            DualScopeStorageMode.Segregated when tenantId is null
                => await hostFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false),
            DualScopeStorageMode.Segregated
                => await RequireTenantFactory().CreateDbContextAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown DualScopeStorageMode: {StorageMode}."),
        };
    }

    /// <summary>
    /// Opens every context that may hold subscriptions visible to the current scope.
    /// Under <see cref="DualScopeStorageMode.Shared"/> returns a single context; under
    /// <see cref="DualScopeStorageMode.Segregated"/> returns both the host and the
    /// active tenant context. Caller disposes every returned context.
    /// </summary>
    /// <remarks>
    /// Used for event fan-out (<c>GetActiveSubscriptionsAsync</c>) where a tenant event
    /// must also reach host SIEM forwarders, and for fetch-then-dispatch writes where
    /// the target scope is unknown at call time.
    /// </remarks>
    public async Task<IReadOnlyList<IWebhooksDbContext>> OpenAllAsync(
        CancellationToken cancellationToken = default)
    {
        IWebhooksDbContext host = await hostFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (StorageMode == DualScopeStorageMode.Shared)
        {
            return [host];
        }

        IWebhooksDbContext tenant = await RequireTenantFactory()
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return [host, tenant];
    }

    /// <summary>
    /// Opens every context that might hold a subscription whose scope is unknown at call
    /// time (writes by <c>Id</c>). Same behaviour as <see cref="OpenAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<IWebhooksDbContext>> OpenForUnknownScopeAsync(
        CancellationToken cancellationToken = default)
        => OpenAllAsync(cancellationToken);

    private IDbContextFactory<WebhooksTenantDbContext> RequireTenantFactory()
    {
        if (tenantFactory is null)
        {
            throw new InvalidOperationException(
                "WebhooksContextResolver: storage mode is Segregated but no IDbContextFactory<WebhooksTenantDbContext> " +
                "is registered. This is a bug in AddGranitWebhooksEntityFrameworkCore.");
        }

        return tenantFactory;
    }
}
