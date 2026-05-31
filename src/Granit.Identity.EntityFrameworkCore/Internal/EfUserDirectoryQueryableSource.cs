using Granit.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserDirectoryQueryableSource"/>.
/// Returns an <see cref="IQueryable{User}"/> over the
/// <see cref="IdentityDbContext.Users"/> set with the framework conventions
/// (tenant + soft-delete) already woven into the model. Composes cleanly
/// with the QueryEngine and OData pipelines per ADR-050 / ADR-051.
/// </summary>
internal sealed class EfUserDirectoryQueryableSource(
    IDbContextFactory<IdentityDbContext> contextFactory)
    : IUserDirectoryQueryableSource, IDisposable
{
    private readonly IdentityDbContext _context = contextFactory.CreateDbContext();

    /// <inheritdoc />
    public IQueryable<User> GetQueryable() =>
        _context.Users.AsNoTracking();

    /// <inheritdoc />
    public void Dispose() => _context.Dispose();
}
