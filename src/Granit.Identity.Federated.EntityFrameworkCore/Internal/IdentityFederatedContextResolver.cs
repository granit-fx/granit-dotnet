using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// Central dispatch helper that hands <see cref="EfCoreUserCacheStore"/> the right
/// <see cref="IIdentityFederatedDbContext"/> for a given scope, independently of the
/// configured <see cref="DualScopeStorageMode"/>.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the contract established by <c>Granit.Webhooks.EntityFrameworkCore</c>'s
/// resolver: inline dispatch, no shared base class. Fetch-then-dispatch is used by the
/// store helpers that receive only an external user id and must probe every scope
/// (<see cref="OpenForUnknownScopeAsync"/>) — e.g. the federated-login lookup path.
/// </para>
/// <para>
/// <see cref="IdentityFederatedHostDbContext"/> serves both modes: under
/// <c>Shared</c> it is the single context holding every identity with the row-level
/// filter active; under <c>Segregated</c> it holds only host identities and the tenant
/// factory points at the companion isolated context.
/// </para>
/// </remarks>
internal sealed class IdentityFederatedContextResolver(
    DualScopeStorageMode storageMode,
    IDbContextFactory<IdentityFederatedHostDbContext> hostFactory,
    IDbContextFactory<IdentityFederatedTenantDbContext>? tenantFactory = null)
{
    public DualScopeStorageMode StorageMode { get; } = storageMode;

    /// <summary>
    /// Opens the appropriate context for an operation whose <c>TenantId</c> is known
    /// up-front.
    /// </summary>
    public async Task<IIdentityFederatedDbContext> OpenForScopeAsync(
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
    /// Opens every context that may hold entries visible to the current scope.
    /// Under <c>Shared</c> returns a single context; under <c>Segregated</c> returns
    /// both the host and the active tenant context. Caller disposes every returned
    /// context.
    /// </summary>
    public async Task<IReadOnlyList<IIdentityFederatedDbContext>> OpenAllAsync(
        CancellationToken cancellationToken = default)
    {
        IIdentityFederatedDbContext host = await hostFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (StorageMode == DualScopeStorageMode.Shared)
        {
            return [host];
        }

        IIdentityFederatedDbContext tenant = await RequireTenantFactory()
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return [host, tenant];
    }

    /// <summary>
    /// Probes every context that might hold an entry whose scope is unknown at call time
    /// (looking up a federated user by their external id only). Same shape as
    /// <see cref="OpenAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<IIdentityFederatedDbContext>> OpenForUnknownScopeAsync(
        CancellationToken cancellationToken = default)
        => OpenAllAsync(cancellationToken);

    private IDbContextFactory<IdentityFederatedTenantDbContext> RequireTenantFactory()
    {
        if (tenantFactory is null)
        {
            throw new InvalidOperationException(
                "IdentityFederatedContextResolver: storage mode is Segregated but no " +
                "IDbContextFactory<IdentityFederatedTenantDbContext> is registered. This is a bug " +
                "in AddGranitIdentityFederatedEntityFrameworkCore.");
        }

        return tenantFactory;
    }
}
