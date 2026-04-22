using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Minimal host DbContext used by the orchestrator integration tests — exposes the
/// authorization entities (<see cref="PermissionGrant"/> and <see cref="RoleMetadata"/>)
/// via <see cref="IPermissionGrantDbContext"/> so the
/// <c>EfCoreRoleMetadataStore&lt;TContext&gt;</c> can be wired against it.
/// </summary>
internal sealed class TestHostDbContext(DbContextOptions<TestHostDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IPermissionGrantDbContext
{
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureAuthorizationModule();
    }
}
