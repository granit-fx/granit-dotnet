using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Stores;

/// <summary>
/// EF Core implementation of <see cref="IPermissionGrantStore"/>.
/// Read queries use <c>AsNoTracking</c> for performance.
/// Write operations handle TOCTOU races via unique-index catch (VULN-205).
/// </summary>
internal sealed class EfCorePermissionGrantStore<TContext>(
    TContext context,
    IGuidGenerator guidGenerator)
    : IPermissionGrantStore
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        context.PermissionGrants
            .AsNoTracking()
            .AnyAsync(
                g => g.TenantId == tenantId
                    && g.Name == permissionName
                    && g.ProviderName == providerName
                    && g.ProviderKey == providerKey,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string providerName,
        string providerKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PermissionGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId
                && g.ProviderName == providerName
                && g.ProviderKey == providerKey)
            .Select(g => g.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGrantedAsync(
        string providerName,
        string providerKey,
        IReadOnlyList<string> permissionNames,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PermissionGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId
                && g.ProviderName == providerName
                && g.ProviderKey == providerKey
                && permissionNames.Contains(g.Name))
            .Select(g => g.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGranteesAsync(
        string providerName,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PermissionGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId
                && g.Name == permissionName
                && g.ProviderName == providerName)
            .Select(g => g.ProviderKey)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> GrantAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        bool exists = await context.PermissionGrants
            .AnyAsync(
                g => g.TenantId == tenantId
                    && g.Name == permissionName
                    && g.ProviderName == providerName
                    && g.ProviderKey == providerKey,
                cancellationToken).ConfigureAwait(false);

        if (exists)
        {
            return false;
        }

        context.PermissionGrants.Add(PermissionGrant.Create(
            guidGenerator.Create(),
            permissionName,
            providerName,
            providerKey,
            tenantId));

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // VULN-205 fix: TOCTOU race — concurrent grant for the same tuple hit
            // the unique index. Treat as idempotent no-op (grant already exists).
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        PermissionGrant? existing = await context.PermissionGrants
            .FirstOrDefaultAsync(
                g => g.TenantId == tenantId
                    && g.Name == permissionName
                    && g.ProviderName == providerName
                    && g.ProviderKey == providerKey,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        existing.MarkAsRevoked();
        context.PermissionGrants.Remove(existing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
