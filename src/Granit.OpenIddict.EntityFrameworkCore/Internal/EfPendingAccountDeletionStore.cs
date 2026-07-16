using Granit.Identity.Local.Domain;
using Granit.Identity.Local.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Services;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

#pragma warning disable EF1001 // IdentityLocalDbContext is internal to the sibling identity package, reached via InternalsVisibleTo

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IPendingAccountDeletionStore"/> over the consolidated
/// <see cref="IdentityLocalDbContext"/>.
/// </summary>
/// <remarks>
/// The reconciliation sweep runs in host context with no active tenant, so it ignores the query
/// filters: the multi-tenant filter (to see every tenant's deletions) and the soft-delete filter
/// (to see the deleted users at all — a soft-deleted row is hidden by default).
/// </remarks>
internal sealed class EfPendingAccountDeletionStore(
    IDbContextFactory<IdentityLocalDbContext> dbFactory) : IPendingAccountDeletionStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingAccountDeletion>> GetPendingAsync(
        int max, CancellationToken cancellationToken = default)
    {
        await using IdentityLocalDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.Set<LocalIdentity>()
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .AsNoTracking()
            .Where(u => u.IsDeleted && u.DeletionEventDispatchedAt == null)
            .OrderBy(u => u.DeletedAt)
            .Take(max)
            .Select(u => new PendingAccountDeletion(u.Id, u.TenantId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MarkDispatchedAsync(
        Guid userId, DateTimeOffset dispatchedAt, CancellationToken cancellationToken = default)
    {
        await using IdentityLocalDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await db.Set<LocalIdentity>()
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.DeletionEventDispatchedAt, dispatchedAt),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
