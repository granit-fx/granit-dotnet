using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Services;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IPendingRegistrationStore"/> over the consolidated
/// <see cref="OpenIddictDbContext"/>.
/// </summary>
/// <remarks>
/// The reconciliation sweep runs in host context with no active tenant, so it ignores the
/// multi-tenant filter to see every tenant's pending registrations. The soft-delete filter is left
/// active — a registration event for a since-deleted account is moot, so a soft-deleted row is
/// correctly hidden.
/// </remarks>
internal sealed class EfPendingRegistrationStore(
    IDbContextFactory<OpenIddictDbContext> dbFactory) : IPendingRegistrationStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingRegistration>> GetPendingAsync(
        int max, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.Set<LocalIdentity>()
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .AsNoTracking()
            .Where(u => u.RegistrationEventPendingSince != null)
            .OrderBy(u => u.RegistrationEventPendingSince)
            .Take(max)
            .Select(u => new PendingRegistration(u.Id, u.TenantId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MarkDispatchedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await db.Set<LocalIdentity>()
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.RegistrationEventPendingSince, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
