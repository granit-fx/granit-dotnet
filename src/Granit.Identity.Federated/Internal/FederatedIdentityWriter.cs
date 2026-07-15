using Granit.Guids;
using Granit.Identity.Domain;
using Granit.Identity.Federated.Domain;
using Granit.MultiTenancy;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// The single write path for the federated user cache. Every ingestion route — cache-aside
/// lookup, provider webhook events, and login-time claim sync — funnels through here so the
/// ADR-051 joint-hydration invariant (a <c>FederatedIdentity</c> row always has a matching
/// canonical <c>User</c>, with the aligned identifier triple
/// <c>FederatedIdentity.Id = FederatedIdentity.UserId = User.Id</c>) is enforced in one place.
/// </summary>
internal interface IFederatedIdentityWriter
{
    /// <summary>Finds the tenant-scoped cache entry for the provider user, then writes it.</summary>
    Task<FederatedIdentity> SyncAsync(IIdentityUser providerUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Joint hydration of the cache row and its canonical <c>User</c>. Callers that already
    /// resolved the existing row pass it to avoid a second lookup. Returns the written entry.
    /// </summary>
    Task<FederatedIdentity> WriteAsync(
        IIdentityUser providerUser, FederatedIdentity? existing, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IFederatedIdentityWriter"/>
/// <remarks>
/// On the insert path the canonical <see cref="User"/> is created first (mirroring the local-side
/// pattern) so admin grids and BI exports see the row immediately; if the cache insert then fails,
/// the freshly-created user is hard-deleted to keep the canonical directory consistent. On the
/// update path only the cache row is touched — the identifier pair is preserved so the canonical
/// user keeps resolving via <see cref="FederatedIdentity.UserId"/>.
/// </remarks>
internal sealed class FederatedIdentityWriter(
    IUserCacheStore store,
    IUserDirectoryWriter userDirectoryWriter,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : IFederatedIdentityWriter
{
    /// <inheritdoc/>
    public async Task<FederatedIdentity> SyncAsync(
        IIdentityUser providerUser, CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        FederatedIdentity? existing = await store
            .FindByExternalIdAsync(providerUser.UserId, tenantId, cancellationToken).ConfigureAwait(false);
        return await WriteAsync(providerUser, existing, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<FederatedIdentity> WriteAsync(
        IIdentityUser providerUser, FederatedIdentity? existing, CancellationToken cancellationToken = default)
    {
        if (existing is not null)
        {
            // Update path — preserve the existing identifier pair so the canonical User row
            // continues to resolve via FederatedIdentity.UserId.
            FederatedIdentity entry = ToCacheEntry(providerUser);
            entry.Id = existing.Id;
            entry.UserId = existing.UserId;
            await store.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
            return entry;
        }

        // Insert path — generate one Guid up front, materialise the canonical User first, then
        // add the federated cache row (with compensation if the cache insert fails).
        FederatedIdentity newEntry = ToCacheEntry(providerUser);

        var canonical = User.Create(
            id: newEntry.Id,
            email: providerUser.Email ?? string.Empty,
            displayName: ResolveDisplayName(providerUser),
            firstName: providerUser.FirstName,
            lastName: providerUser.LastName,
            tenantId: newEntry.TenantId);

        await userDirectoryWriter.CreateAsync(canonical, cancellationToken).ConfigureAwait(false);

        try
        {
            await store.UpsertAsync(newEntry, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await userDirectoryWriter.DeleteAsync(newEntry.Id, cancellationToken).ConfigureAwait(false);
            throw;
        }

        return newEntry;
    }

    private static string ResolveDisplayName(IIdentityUser providerUser)
    {
        string composed = $"{providerUser.FirstName} {providerUser.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(composed))
        {
            return composed;
        }

        return !string.IsNullOrWhiteSpace(providerUser.Email)
            ? providerUser.Email
            : providerUser.Username ?? providerUser.UserId;
    }

    private FederatedIdentity ToCacheEntry(IIdentityUser user)
    {
        // Per ADR-051 B-step 3, pre-assign the Guid so FederatedIdentity.UserId can be aligned
        // with .Id at construction time. The store preserves these identifiers on update — only an
        // INSERT path adopts the freshly-generated value, so the alignment invariant
        // (FederatedIdentity.Id == FederatedIdentity.UserId == User.Id) holds from the first row.
        Guid id = guidGenerator.Create();
        return new FederatedIdentity
        {
            Id = id,
            UserId = id,
            ExternalUserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Enabled = user.Enabled,
            LastSyncedAt = timeProvider.GetUtcNow(),
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
        };
    }
}
