using Granit.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserDirectoryWriter"/>. Opens
/// a short-lived <see cref="IdentityDbContext"/> per call (factory
/// pattern, mirroring <see cref="EfUserDirectoryQueryableSource"/>) and
/// delegates to <see cref="DbSet{TEntity}.AddAsync"/> +
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>.
/// </summary>
internal sealed class EfUserDirectoryWriter(
    IDbContextFactory<IdentityDbContext> contextFactory)
    : IUserDirectoryWriter
{
    /// <inheritdoc />
    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using IdentityDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.Users
            .Where(u => u.Id == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
