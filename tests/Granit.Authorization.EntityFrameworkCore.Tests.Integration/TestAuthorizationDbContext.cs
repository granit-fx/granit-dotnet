using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Minimal host DbContext used by the PG integration tests — exposes
/// <see cref="PermissionGrant"/> and <see cref="RoleMetadata"/> through the
/// framework's <see cref="IPermissionGrantDbContext"/> contract and wires both
/// entity configurations via <see cref="PermissionGrantModelBuilderExtensions.ConfigureAuthorizationModule"/>.
/// </summary>
internal sealed class TestAuthorizationDbContext(
    DbContextOptions<TestAuthorizationDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IPermissionGrantDbContext
{
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureAuthorizationModule();
    }
}
