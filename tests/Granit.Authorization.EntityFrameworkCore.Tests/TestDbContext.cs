using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

/// <summary>
/// Shared in-memory test DbContext for Granit.Authorization.EntityFrameworkCore tests.
/// Must be internal (not private) so that Castle.DynamicProxy can proxy
/// ILogger&lt;PermissionManager&lt;TestDbContext&gt;&gt; via NSubstitute.
/// </summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IPermissionGrantDbContext
{
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureAuthorizationModule();
}
