using Granit.OpenIddict.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods to configure the OpenIddict entity model (applications, authorizations, scopes,
/// tokens and the signing-key table). The local-identity tables are configured separately by
/// <c>ConfigureGranitIdentityLocal</c> in <c>Granit.Identity.Local.EntityFrameworkCore</c>.
/// </summary>
public static class OpenIddictModelBuilderExtensions
{
    /// <summary>
    /// Configures the OpenIddict entities and the signing-key table: OpenIddict key/index conventions,
    /// the <c>openiddict_*</c> table prefix and column constraints. A host migration context that owns
    /// the whole schema calls this alongside <c>ConfigureGranitIdentityLocal</c>.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    public static ModelBuilder ConfigureGranitOpenIddict(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        string prefix = GranitOpenIddictDbProperties.DbTablePrefix;
        string? schema = GranitOpenIddictDbProperties.DbSchema;

        // ──── OpenIddict conventions + table remapping ────
        // UseOpenIddict registers key/index conventions for the custom OpenIddict entities.
        // Must be called before ToTable remapping.
        modelBuilder.UseOpenIddict<GranitOpenIddictApplication, GranitOpenIddictAuthorization,
            GranitOpenIddictScope, GranitOpenIddictToken, Guid>();

        modelBuilder.Entity<GranitOpenIddictApplication>().ToTable(prefix + "applications", schema);
        modelBuilder.Entity<GranitOpenIddictAuthorization>().ToTable(prefix + "authorizations", schema);
        modelBuilder.Entity<GranitOpenIddictScope>().ToTable(prefix + "scopes", schema);
        modelBuilder.Entity<GranitOpenIddictToken>().ToTable(prefix + "tokens", schema);

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

            // At most one Active key per type. Makes concurrent first-boot generation race-safe:
            // when two replicas boot against an empty store, the second replica's insert of a
            // duplicate active key is rejected by the database (SigningKeyRefreshService swallows
            // the loss and reloads the winner's keys). Status is persisted as its PascalCase string.
            b.HasIndex(k => k.KeyType)
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'")
                .HasDatabaseName($"uq_{prefix}signing_keys_active_type");
        });

        return modelBuilder;
    }
}
