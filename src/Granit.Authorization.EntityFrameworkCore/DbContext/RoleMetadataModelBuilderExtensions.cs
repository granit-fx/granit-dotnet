using Granit.Authorization.Domain;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.DbContext;

/// <summary>EF Core model builder extensions for <see cref="RoleMetadata"/>.</summary>
public static class RoleMetadataModelBuilderExtensions
{
    /// <summary>
    /// Applies the entity configuration for <see cref="RoleMetadata"/>: host-level table
    /// name, column constraints, unique composite index on <c>(Name, TenantId, ClientId)</c>
    /// with PostgreSQL <c>NULLS NOT DISTINCT</c> semantics, and a CHECK constraint enforcing
    /// the <c>MultiTenancySides</c> ↔ <c>TenantId</c> invariant at the database level.
    /// </summary>
    /// <remarks>
    /// Call from <c>OnModelCreating</c> in the host application's DbContext alongside
    /// <see cref="PermissionGrantModelBuilderExtensions.ConfigureAuthorizationModule"/>.
    /// </remarks>
    public static ModelBuilder ConfigureRoleMetadata(this ModelBuilder builder)
    {
        builder.Entity<RoleMetadata>(entity =>
        {
            entity.ToTable(
                GranitAuthorizationDbProperties.DbTablePrefix + "role_metadata",
                GranitAuthorizationDbProperties.DbSchema,
                table => table.HasCheckConstraint(
                    $"ck_{GranitAuthorizationDbProperties.DbTablePrefix}role_metadata_side_tenant_consistency",
                    // Side values: Host = 1, Tenant = 2, Both = 3 (Host | Tenant)
                    $"(\"{nameof(RoleMetadata.MultiTenancySides)}\" = {(int)MultiTenancySides.Host} AND \"{nameof(RoleMetadata.TenantId)}\" IS NULL)" +
                    $" OR (\"{nameof(RoleMetadata.MultiTenancySides)}\" = {(int)MultiTenancySides.Both} AND \"{nameof(RoleMetadata.TenantId)}\" IS NULL)" +
                    $" OR (\"{nameof(RoleMetadata.MultiTenancySides)}\" = {(int)MultiTenancySides.Tenant} AND \"{nameof(RoleMetadata.TenantId)}\" IS NOT NULL)"));

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ClientId).HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2048);
            entity.Property(e => e.MultiTenancySides).IsRequired();
            entity.Property(e => e.IsSystem).IsRequired();
            entity.Property(e => e.IsOrphaned).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.OrphanedAt);

            entity.HasIndex(e => new { e.Name, e.TenantId, e.ClientId })
                  .IsUnique()
                  .AreNullsDistinct(false)
                  .HasDatabaseName($"uq_{GranitAuthorizationDbProperties.DbTablePrefix}role_metadata_name_tenant_client");
        });

        return builder;
    }
}
