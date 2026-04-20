using Granit.Authorization.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.DbContext;

/// <summary>EF Core model builder extensions for the Authorization module.</summary>
public static class PermissionGrantModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Authorization module.
    /// </summary>
    /// <remarks>
    /// Configures the <see cref="PermissionGrant"/> entity: host-level table name, column
    /// constraints, and unique composite index on
    /// <c>(TenantId, ProviderName, ProviderKey, Name)</c>. Call this from
    /// <c>OnModelCreating</c> in the host application's DbContext.
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
            entity.Property(e => e.ProviderName).HasMaxLength(8).IsRequired();
            entity.Property(e => e.ProviderKey).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ProviderName, e.ProviderKey, e.Name })
                  .IsUnique()
                  .HasDatabaseName($"uq_{GranitAuthorizationDbProperties.DbTablePrefix}permission_grants_tenant_provider_key_name");
        });

        return builder;
    }
}
