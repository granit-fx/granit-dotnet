using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Central dispatch helper that hands every Identity store the right
/// <see cref="IIdentityDbContext"/> for a given scope, independently of the configured
/// <see cref="DualScopeStorageMode"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063 design decisions for Epic #2382 V2, dispatch is inline (no shared base
/// class) and uses fan-out for writes whose target scope is unknown at call time (delete
/// by user id). Cross-tenant aggregation for host-admin reads is preserved under
/// <see cref="DualScopeStorageMode.Segregated"/> via <see cref="OpenAllAsync"/>.
/// </para>
/// <para>
/// <see cref="IdentityHostDbContext"/> serves both modes: under <c>Shared</c> it is the
/// single context holding every user with the row-level filter active; under
/// <c>Segregated</c> it holds only host-admin users and the tenant factory points at the
/// companion isolated context.
/// </para>
/// </remarks>
internal sealed class IdentityContextResolver(
    DualScopeStorageMode storageMode,
    IDbContextFactory<IdentityHostDbContext> hostFactory,
    IDbContextFactory<IdentityTenantDbContext>? tenantFactory = null)
{
    /// <summary>The active storage mode declared at registration.</summary>
    public DualScopeStorageMode StorageMode { get; } = storageMode;

    /// <summary>
    /// Opens the appropriate context for an operation whose <c>TenantId</c> is known
    /// up-front (e.g. an incoming write whose scope is set by the caller).
    /// </summary>
    public async Task<IIdentityDbContext> OpenForScopeAsync(
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
    /// Opens every context that may hold users visible to the current scope. Under
    /// <see cref="DualScopeStorageMode.Shared"/> returns a single context; under
    /// <see cref="DualScopeStorageMode.Segregated"/> returns both the host and the active
    /// tenant context. Caller disposes every returned context.
    /// </summary>
    /// <remarks>
    /// Used for host-admin cross-tenant reads and fan-out writes (delete by id) where the
    /// target scope is unknown at call time.
    /// </remarks>
    public async Task<IReadOnlyList<IIdentityDbContext>> OpenAllAsync(
        CancellationToken cancellationToken = default)
    {
        IIdentityDbContext host = await hostFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (StorageMode == DualScopeStorageMode.Shared)
        {
            return [host];
        }

        IIdentityDbContext tenant = await RequireTenantFactory()
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return [host, tenant];
    }

    /// <summary>
    /// Opens every context that might hold a user whose scope is unknown at call time
    /// (writes by id). Same behaviour as <see cref="OpenAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<IIdentityDbContext>> OpenForUnknownScopeAsync(
        CancellationToken cancellationToken = default)
        => OpenAllAsync(cancellationToken);

    private IDbContextFactory<IdentityTenantDbContext> RequireTenantFactory()
    {
        if (tenantFactory is null)
        {
            throw new InvalidOperationException(
                "IdentityContextResolver: storage mode is Segregated but no IDbContextFactory<IdentityTenantDbContext> " +
                "is registered. This is a bug in AddGranitIdentityEntityFrameworkCore.");
        }

        return tenantFactory;
    }
}
