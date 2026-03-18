using Granit.Identity.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.DbContext;

/// <summary>
/// Implement this interface on the host application's <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// to enable Granit.Identity.EntityFrameworkCore persistence.
/// Call <see cref="UserCacheModelBuilderExtensions.ConfigureIdentityModule"/> in <c>OnModelCreating</c>.
/// </summary>
public interface IUserCacheDbContext
{
    /// <summary>Identity user cache entries table.</summary>
    DbSet<UserCacheEntry> UserCacheEntries { get; }
}
