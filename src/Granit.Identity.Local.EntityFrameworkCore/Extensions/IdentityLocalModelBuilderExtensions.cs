using System.Linq.Expressions;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Local.EntityFrameworkCore.Extensions;

/// <summary>
/// Configures the local-identity entity model: the ASP.NET Core Identity tables (users, roles,
/// claims, logins, tokens, passkeys), the group tables, their table prefix, column constraints,
/// indexes, relationships and the manual soft-delete filter for <see cref="LocalIdentity"/>.
/// </summary>
public static class IdentityLocalModelBuilderExtensions
{
    /// <summary>
    /// Applies the local-identity model to <paramref name="modelBuilder"/>. Self-contained — declares
    /// the identity schema explicitly (keys, unique indexes, maxlengths, concurrency tokens and join
    /// relationships) rather than relying on <c>IdentityDbContext.OnModelCreating</c>, so it composes
    /// into any <c>GranitDbContext</c>. Names and lengths mirror the framework defaults byte-for-byte
    /// (relational-model pinning-test guarded).
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="dataFilter">
    /// Data filter for service-level soft-delete bypass. If <c>null</c> the soft-delete filter is
    /// always applied (bypass only via <c>IgnoreQueryFilters</c>).
    /// </param>
    /// <param name="extensionOptions">
    /// Dynamic user extension options — adds shadow-property columns on the users table for each mapping.
    /// </param>
    public static ModelBuilder ConfigureGranitIdentityLocal(
        this ModelBuilder modelBuilder,
        IDataFilter? dataFilter = null,
        MetadataMappingOptions<LocalIdentity>? extensionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        string prefix = GranitIdentityLocalDbProperties.DbTablePrefix;
        string? schema = GranitIdentityLocalDbProperties.DbSchema;

        SoftDeleteProxy proxy = new(dataFilter);

        modelBuilder.Entity<LocalIdentity>(b =>
        {
            b.ToTable(prefix + "users", schema);
            b.Property(u => u.FirstName).HasMaxLength(256);
            b.Property(u => u.LastName).HasMaxLength(256);
            b.Property(u => u.DeletedBy).HasMaxLength(256);
            b.Property(u => u.CreatedBy).HasMaxLength(256);
            b.Property(u => u.ModifiedBy).HasMaxLength(256);

            // ── Replicated IdentityDbContext<TUser,TRole,TKey> conventions ──
            // This model composes into GranitDbContext, not IdentityDbContext, so the identity schema
            // (indexes, maxlengths, concurrency token, join relationships) is declared here. Names and
            // lengths mirror the framework defaults byte-for-byte (pinning-test guarded).
            b.Property(u => u.UserName).HasMaxLength(256);
            b.Property(u => u.NormalizedUserName).HasMaxLength(256);
            b.Property(u => u.Email).HasMaxLength(256);
            b.Property(u => u.NormalizedEmail).HasMaxLength(256);
            b.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
            b.HasIndex(u => u.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique();
            b.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex");
            b.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(uc => uc.UserId).IsRequired();
            b.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(ul => ul.UserId).IsRequired();
            b.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(ut => ut.UserId).IsRequired();
            b.HasMany<IdentityUserRole<Guid>>().WithOne().HasForeignKey(ur => ur.UserId).IsRequired();

            // Manual soft-delete filter with IDataFilter bypass support. LocalIdentity does NOT
            // implement ISoftDeletable (incompatible with UserManager), so ApplyGranitConventions
            // cannot register this filter automatically. Pattern: bypass || real (!IsDeleted).
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

            // Replicated IdentityDbContext role conventions (see the LocalIdentity block).
            b.Property(r => r.Name).HasMaxLength(256);
            b.Property(r => r.NormalizedName).HasMaxLength(256);
            b.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();
            b.HasIndex(r => r.NormalizedName).HasDatabaseName("RoleNameIndex").IsUnique();
            b.HasMany<IdentityUserRole<Guid>>().WithOne().HasForeignKey(ur => ur.RoleId).IsRequired();
            b.HasMany<IdentityRoleClaim<Guid>>().WithOne().HasForeignKey(rc => rc.RoleId).IsRequired();
        });

        // ASP.NET Identity join/claim entities — keys defined explicitly so this model is
        // self-contained in any DbContext, not only IdentityDbContext-derived ones.
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
    /// EF Core evaluates property access on a <see cref="ConstantExpression"/> as a query parameter
    /// re-evaluated per query. This proxy mirrors <c>ApplyGranitConventions</c>'s internal filter proxy
    /// for the <see cref="ISoftDeletable"/> filter needed by <see cref="LocalIdentity"/>.
    /// </summary>
    private sealed class SoftDeleteProxy(IDataFilter? dataFilter)
    {
        public bool SoftDeleteEnabled => dataFilter?.IsEnabled<ISoftDeletable>() ?? true;
    }
}
