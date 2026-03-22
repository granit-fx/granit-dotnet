using Granit.Identity.Federated.EntityFrameworkCore.DbContext;
using Granit.Identity.Federated.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory test DbContext for Granit.Identity.Federated.EntityFrameworkCore tests.
/// Internal for Castle.DynamicProxy compatibility with NSubstitute.
/// </summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IUserCacheDbContext
{
    public DbSet<UserCacheEntry> UserCacheEntries => Set<UserCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureIdentityModule();
}
