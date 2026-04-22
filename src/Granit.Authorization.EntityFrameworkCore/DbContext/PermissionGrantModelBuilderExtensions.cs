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
    /// <para>
    /// Configures the <see cref="PermissionGrant"/> entity: host-level table name, column
    /// constraints, and unique composite index on
    /// <c>(TenantId, ProviderName, ProviderKey, Name)</c>. Also wires the
    /// <see cref="RoleMetadata"/> configuration via <see cref="RoleMetadataModelBuilderExtensions.ConfigureRoleMetadata"/>.
    /// Call this from <c>OnModelCreating</c> in the host application's DbContext.
    /// </para>
    /// <para>
    /// The unique index uses PostgreSQL <c>NULLS NOT DISTINCT</c> semantics (via the
    /// <c>Npgsql:NullsDistinct</c> annotation) so host-level grants with
    /// <c>TenantId = null</c> cannot duplicate each other on the same
    /// <c>(ProviderName, ProviderKey, Name)</c> tuple.
    /// </para>
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
                  .HasAnnotation("Npgsql:NullsDistinct", false)
                  .HasDatabaseName($"uq_{GranitAuthorizationDbProperties.DbTablePrefix}permission_grants_tenant_provider_key_name");
        });

        builder.ConfigureRoleMetadata();

        return builder;
    }
}
