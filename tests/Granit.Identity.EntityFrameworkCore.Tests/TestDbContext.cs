using Granit.Identity.EntityFrameworkCore.DbContext;
using Granit.Identity.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory test DbContext for Granit.Identity.EntityFrameworkCore tests.
/// Internal for Castle.DynamicProxy compatibility with NSubstitute.
/// </summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IUserCacheDbContext
{
    public DbSet<UserCacheEntry> UserCacheEntries => Set<UserCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureIdentityModule();
}
