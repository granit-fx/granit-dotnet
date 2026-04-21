using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Stores;

/// <summary>
/// EF Core implementation of <see cref="IRoleMetadataStore"/>.
/// </summary>
/// <remarks>
/// Read queries use <c>AsNoTracking</c> by default; writes expect the caller to have
/// already loaded or constructed the aggregate (see <c>IGranitRoleOrchestrator</c>).
/// </remarks>
internal sealed class EfCoreRoleMetadataStore<TContext>(TContext context)
    : IRoleMetadataStore
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    /// <inheritdoc />
    public Task<RoleMetadata?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RoleMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<RoleMetadata?> FindByNameAsync(
        string name,
        Guid? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default) =>
        context.RoleMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Name == name && r.TenantId == tenantId && r.ClientId == clientId,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleMetadata>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await context.RoleMetadata
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddAsync(RoleMetadata role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        context.RoleMetadata.Add(role);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RoleMetadata role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        context.RoleMetadata.Update(role);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(RoleMetadata role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        role.MarkAsDeleted();
        context.RoleMetadata.Remove(role);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
