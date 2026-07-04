using System.Linq.Expressions;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods to configure the OpenIddict module's entity model.
/// </summary>
public static class OpenIddictModelBuilderExtensions
{
    /// <summary>
    /// Configures the OpenIddict module entities: table prefixes, column constraints,
    /// indexes, and the manual soft-delete filter for <see cref="LocalIdentity"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called AFTER <c>base.OnModelCreating()</c> (Identity conventions)
    /// and <c>modelBuilder.UseOpenIddict&lt;Guid&gt;()</c> (OpenIddict conventions),
    /// but BEFORE <c>modelBuilder.ApplyGranitConventions()</c>.
    /// </para>
    /// <para>
    /// <see cref="LocalIdentity"/> cannot implement <see cref="ISoftDeletable"/> because
    /// ASP.NET Core Identity's <c>UserManager&lt;T&gt;</c> uses reflection/metadata
    /// patterns that are incompatible with the interface. The soft-delete filter is
    /// therefore registered manually here, using the same <c>bypass || real</c> pattern
    /// as <c>ApplyGranitConventions</c> so that <c>IDataFilter.Disable&lt;ISoftDeletable&gt;()</c>
    /// works consistently across all entities.
    /// </para>
    /// </remarks>
    /// <param name="modelBuilder">The EF Core ModelBuilder.</param>
    /// <param name="dataFilter">
    /// Data filter service for service-level bypass. If <c>null</c>, the soft-delete filter
    /// is always applied (no bypass possible except via <c>IgnoreQueryFilters</c>).
    /// </param>
    /// <param name="extensionOptions">
    /// Dynamic user extension options. If provided, adds Shadow Properties as SQL columns
    /// on the <c>openiddict_users</c> table for each mapped property.
    /// </param>
    public static ModelBuilder ConfigureOpenIddictModule(
        this ModelBuilder modelBuilder,
        IDataFilter? dataFilter = null,
        MetadataMappingOptions<LocalIdentity>? extensionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        string prefix = GranitOpenIddictDbProperties.DbTablePrefix;
        string? schema = GranitOpenIddictDbProperties.DbSchema;

        // ──── Remap ASP.NET Identity tables to openiddict_* prefix ────

        SoftDeleteProxy proxy = new(dataFilter);

        modelBuilder.Entity<LocalIdentity>(b =>
        {
            b.ToTable(prefix + "users", schema);
            b.Property(u => u.FirstName).HasMaxLength(256);
            b.Property(u => u.LastName).HasMaxLength(256);
            b.Property(u => u.DeletedBy).HasMaxLength(256);
            b.Property(u => u.CreatedBy).HasMaxLength(256);
            b.Property(u => u.ModifiedBy).HasMaxLength(256);

            // Manual soft-delete filter with IDataFilter bypass support.
            // LocalIdentity does NOT implement ISoftDeletable (incompatible with UserManager),
            // so ApplyGranitConventions cannot register this filter automatically.
            // Pattern: bypass (IDataFilter disabled) || real (!IsDeleted)
            ParameterExpression param = Expression.Parameter(typeof(LocalIdentity), "u");
            UnaryExpression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(SoftDeleteProxy.SoftDeleteEnabled)));
            UnaryExpression notDeleted = Expression.Not(
                Expression.Property(param, nameof(LocalIdentity.IsDeleted)));
            var filter =
                Expression.Lambda<Func<LocalIdentity, bool>>(Expression.OrElse(bypass, notDeleted), param);

            b.HasQueryFilter(Granit.Persistence.EntityFrameworkCore.GranitFilterNames.SoftDelete, filter);

            b.Property(u => u.ConsecutiveLockouts).HasDefaultValue(0);

            b.HasIndex(u => u.TenantId)
                .HasDatabaseName($"ix_{prefix}users_tenant_id");
        });

        modelBuilder.Entity<GranitRole>(b =>
        {
            b.ToTable(prefix + "roles", schema);
            b.Property(r => r.Description).HasMaxLength(512);
        });

        // ASP.NET Identity join/claim entities — define keys explicitly so that
        // ConfigureOpenIddictModule() is self-contained and works in any DbContext,
        // not only those inheriting from IdentityDbContext.
        modelBuilder.Entity<IdentityUserRole<Guid>>(b =>
        {
            b.ToTable(prefix + "user_roles", schema);
            b.HasKey(r => new { r.UserId, r.RoleId });
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable(prefix + "user_claims", schema);
            b.HasKey(c => c.Id);
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable(prefix + "user_logins", schema);
            b.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable(prefix + "user_tokens", schema);
            b.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b =>
        {
            b.ToTable(prefix + "role_claims", schema);
            b.HasKey(c => c.Id);
        });

        // AspNetCore Identity Version3: passkey (WebAuthn/FIDO2) credentials.
        // IdentityDbContext.OnModelCreating adds this entity when SchemaVersion = Version3;
        // we add it here too so that host DbContexts that do not inherit from IdentityDbContext
        // (e.g. ShowcaseHostDbContext) include the table in their migration.
        // For OpenIddictDbContext the ToTable call below overrides the default AspNetUserPasskeys name.
        modelBuilder.Entity<IdentityUserPasskey<Guid>>(b =>
        {
            b.ToTable(prefix + "user_passkeys", schema);
            b.HasKey(p => p.CredentialId);
            b.HasOne<LocalIdentity>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.OwnsOne(p => p.Data, data => data.ToJson());
        });

        // ──── OpenIddict conventions + table remapping ────
        // UseOpenIddict registers key/index conventions for the custom OpenIddict entities.
        // Must be called before ToTable remapping.
        modelBuilder.UseOpenIddict<GranitOpenIddictApplication, GranitOpenIddictAuthorization,
            GranitOpenIddictScope, GranitOpenIddictToken, Guid>();

        modelBuilder.Entity<GranitOpenIddictApplication>().ToTable(prefix + "applications", schema);
        modelBuilder.Entity<GranitOpenIddictAuthorization>().ToTable(prefix + "authorizations", schema);
        modelBuilder.Entity<GranitOpenIddictScope>().ToTable(prefix + "scopes", schema);
        modelBuilder.Entity<GranitOpenIddictToken>().ToTable(prefix + "tokens", schema);

        // ──── Custom group tables ────

        modelBuilder.Entity<GranitUserGroup>(b =>
        {
            b.ToTable(prefix + "user_groups", schema);
            b.Property(g => g.Name).HasMaxLength(256).IsRequired();
            b.Property(g => g.Description).HasMaxLength(512);

            b.HasIndex(g => new { g.TenantId, g.Name })
                .IsUnique()
                .HasDatabaseName($"uq_{prefix}user_groups_tenant_name");
        });

        modelBuilder.Entity<GranitUserGroupMember>(b =>
        {
            b.ToTable(prefix + "user_group_members", schema);

            b.HasIndex(m => new { m.GroupId, m.UserId })
                .IsUnique()
                .HasDatabaseName($"uq_{prefix}user_group_members_group_user");

            b.HasIndex(m => m.UserId)
                .HasDatabaseName($"ix_{prefix}user_group_members_user_id");
        });

        // ──── Signing key table ────

        modelBuilder.Entity<SigningKey>(b =>
        {
            b.ToTable(prefix + "signing_keys", schema);
            b.Property(k => k.KeyId).HasMaxLength(128).IsRequired();
            b.Property(k => k.KeyType).HasMaxLength(32).IsRequired();
            b.Property(k => k.Algorithm).HasMaxLength(32).IsRequired();
            b.Property(k => k.EncryptedKeyMaterial).IsRequired();
            b.Property(k => k.CreatedBy).HasMaxLength(256);

            b.HasIndex(k => k.KeyId)
                .IsUnique()
                .HasDatabaseName($"uq_{prefix}signing_keys_key_id");

            b.HasIndex(k => new { k.KeyType, k.Status })
                .HasDatabaseName($"ix_{prefix}signing_keys_type_status");
        });

        // ──── Dynamic user extension columns ────

        if (extensionOptions is { Mappings.Count: > 0 })
        {
            modelBuilder.Entity<LocalIdentity>(b =>
            {
                foreach (MetadataMapping mapping in extensionOptions.Mappings)
                {
                    Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder prop =
                        b.Property(mapping.ClrType, mapping.Name);

                    if (mapping.MaxLength.HasValue)
                    {
                        prop.HasMaxLength(mapping.MaxLength.Value);
                    }

                    if (mapping.IsRequired)
                    {
                        prop.IsRequired();
                    }
                }
            });
        }

        return modelBuilder;
    }

    /// <summary>
    /// EF Core evaluates property access on a <see cref="ConstantExpression"/> as a query
    /// parameter re-evaluated on each query. This proxy mirrors the pattern used by
    /// <c>ApplyGranitConventions</c>'s internal <c>FilterProxy</c>, but only for the
    /// <see cref="ISoftDeletable"/> filter needed by <see cref="LocalIdentity"/>.
    /// </summary>
    private sealed class SoftDeleteProxy(IDataFilter? dataFilter)
    {
        public bool SoftDeleteEnabled => dataFilter?.IsEnabled<ISoftDeletable>() ?? true;
    }
}
