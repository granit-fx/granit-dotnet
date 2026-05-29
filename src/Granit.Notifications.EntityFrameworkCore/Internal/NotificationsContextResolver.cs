using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// Central dispatch helper that hands every Notifications store the right
/// <see cref="INotificationsDbContext"/> for a given scope, independently of the
/// configured <see cref="DualScopeStorageMode"/>.
/// </summary>
internal sealed class NotificationsContextResolver(
    DualScopeStorageMode storageMode,
    IDbContextFactory<NotificationsHostDbContext> hostFactory,
    IDbContextFactory<NotificationsTenantDbContext>? tenantFactory = null)
{
    /// <summary>The active storage mode declared at registration.</summary>
    public DualScopeStorageMode StorageMode { get; } = storageMode;

    /// <summary>
    /// Opens the appropriate context for an operation whose <c>TenantId</c> is known
    /// up-front (the caller knows whether the write targets host or tenant scope).
    /// </summary>
    public async Task<INotificationsDbContext> OpenForScopeAsync(
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
    /// Opens every context that may hold notifications visible to the current scope.
    /// Under <see cref="DualScopeStorageMode.Shared"/> returns a single context; under
    /// <see cref="DualScopeStorageMode.Segregated"/> returns both the host and the active
    /// tenant context. Caller disposes every returned context.
    /// </summary>
    public async Task<IReadOnlyList<INotificationsDbContext>> OpenAllAsync(
        CancellationToken cancellationToken = default)
    {
        INotificationsDbContext host = await hostFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (StorageMode == DualScopeStorageMode.Shared)
        {
            return [host];
        }

        INotificationsDbContext tenant = await RequireTenantFactory()
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return [host, tenant];
    }

    /// <summary>
    /// Opens every context that might hold a notification whose scope is unknown at call
    /// time (writes by id). Same behaviour as <see cref="OpenAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<INotificationsDbContext>> OpenForUnknownScopeAsync(
        CancellationToken cancellationToken = default)
        => OpenAllAsync(cancellationToken);

    private IDbContextFactory<NotificationsTenantDbContext> RequireTenantFactory()
    {
        if (tenantFactory is null)
        {
            throw new InvalidOperationException(
                "NotificationsContextResolver: storage mode is Segregated but no IDbContextFactory<NotificationsTenantDbContext> " +
                "is registered. This is a bug in AddGranitNotificationsEntityFrameworkCore.");
        }

        return tenantFactory;
    }
}
