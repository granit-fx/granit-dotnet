using Granit.Authorization.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.DbContext;

/// <summary>EF Core model builder extensions for the Authorization module.</summary>
public static class PermissionGrantModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Authorization module.
    /// </summary>
    /// <remarks>
    /// Configures the <see cref="PermissionGrant"/> entity: table name, column constraints,
    /// and unique composite index on (TenantId, Name, RoleName).
    /// Call this from <c>OnModelCreating</c> in the host application's DbContext.
    /// </remarks>
    public static ModelBuilder ConfigureAuthorizationModule(this ModelBuilder builder)
    {
        builder.Entity<PermissionGrant>(entity =>
        {
            entity.ToTable(
                GranitAuthorizationDbProperties.DbTablePrefix + "permission_grants",
                GranitAuthorizationDbProperties.DbSchema);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.RoleName).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Name, e.RoleName })
                  .IsUnique()
                  .HasDatabaseName($"uq_{GranitAuthorizationDbProperties.DbTablePrefix}permission_grants_tenant_name_role");
        });

        return builder;
    }
}
