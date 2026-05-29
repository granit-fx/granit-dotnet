using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Central dispatch helper that hands every Auditing service the right
/// <see cref="IAuditingDbContext"/> for a given scope, independently of the configured
/// <see cref="DualScopeStorageMode"/>.
/// </summary>
internal sealed class AuditingContextResolver(
    DualScopeStorageMode storageMode,
    IDbContextFactory<AuditingHostDbContext> hostFactory,
    IDbContextFactory<AuditingTenantDbContext>? tenantFactory = null)
{
    /// <summary>The active storage mode declared at registration.</summary>
    public DualScopeStorageMode StorageMode { get; } = storageMode;

    /// <summary>
    /// Opens the appropriate context for an operation whose <c>TenantId</c> is known
    /// up-front. Under <c>Shared</c> always returns the host context (single physical
    /// table). Under <c>Segregated</c> routes null → host, non-null → tenant.
    /// </summary>
    public async Task<IAuditingDbContext> OpenForScopeAsync(
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
    /// Opens every context that may hold audit entries. Caller disposes each returned
    /// context.
    /// </summary>
    public async Task<IReadOnlyList<IAuditingDbContext>> OpenAllAsync(
        CancellationToken cancellationToken = default)
    {
        IAuditingDbContext host = await hostFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (StorageMode == DualScopeStorageMode.Shared)
        {
            return [host];
        }

        IAuditingDbContext tenant = await RequireTenantFactory()
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return [host, tenant];
    }

    /// <summary>
    /// Opens every context that might hold an audit entry whose scope is unknown at call
    /// time (reads by id). Same behaviour as <see cref="OpenAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<IAuditingDbContext>> OpenForUnknownScopeAsync(
        CancellationToken cancellationToken = default)
        => OpenAllAsync(cancellationToken);

    private IDbContextFactory<AuditingTenantDbContext> RequireTenantFactory()
    {
        if (tenantFactory is null)
        {
            throw new InvalidOperationException(
                "AuditingContextResolver: storage mode is Segregated but no IDbContextFactory<AuditingTenantDbContext> " +
                "is registered. This is a bug in AddGranitAuditingEntityFrameworkCore.");
        }

        return tenantFactory;
    }
}
