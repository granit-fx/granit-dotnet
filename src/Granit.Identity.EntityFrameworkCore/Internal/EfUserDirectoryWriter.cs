using Granit.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserDirectoryWriter"/>. Dispatches through
/// <see cref="IdentityContextResolver"/> so the same store serves both
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> (single
/// context) and <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>
/// (host context + tenant context) deployments.
/// </summary>
/// <remarks>
/// <para><b>Create</b> dispatches on <c>user.TenantId</c> — the caller has already set the
/// scope on the aggregate, so the resolver routes directly to the correct context without a
/// pre-fetch.</para>
/// <para><b>Delete</b> takes an id only, so the writer fans out across every candidate
/// context and issues <see cref="EntityFrameworkQueryableExtensions.ExecuteDeleteAsync"/>
/// against each — the no-op on the non-matching context is cheap and tolerates the
/// compensating-delete contract (deleting a row that was never created must not throw).</para>
/// </remarks>
internal sealed class EfUserDirectoryWriter(IdentityContextResolver resolver)
    : IUserDirectoryWriter
{
    /// <inheritdoc />
    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using IIdentityDbContext context = await resolver
            .OpenForScopeAsync(user.TenantId, cancellationToken).ConfigureAwait(false);

        await context.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IIdentityDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IIdentityDbContext db in contexts)
            {
                await db.Users
                    .Where(u => u.Id == userId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (IIdentityDbContext db in contexts)
            {
                await db.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
