using Granit.Identity.Federated.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.DbContext;

/// <summary>EF Core model builder extensions for the Identity module.</summary>
public static class UserCacheModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Identity module.
    /// </summary>
    /// <remarks>
    /// Configures the <see cref="UserCacheEntry"/> entity: table name, column constraints,
    /// and indexes for efficient lookup and search.
    /// Call this from <c>OnModelCreating</c> in the host application's DbContext.
    /// </remarks>
    public static ModelBuilder ConfigureIdentityModule(this ModelBuilder builder)
    {
        builder.Entity<UserCacheEntry>(entity =>
        {
            entity.ToTable(
                GranitIdentityDbProperties.DbTablePrefix + "user_cache_entries",
                GranitIdentityDbProperties.DbSchema);
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExternalUserId).HasMaxLength(256).IsRequired();

            // PII columns are encrypted at rest via [Encrypted] — the value converter
            // is attached by ApplyEncryptionConventions. Ciphertext is longer than
            // plaintext, so the column widths are widened. 2048 comfortably holds an
            // AES-GCM envelope wrapping the previous plaintext max plus base64 overhead.
            entity.Property(e => e.Username).HasMaxLength(2048);
            entity.Property(e => e.Email).HasMaxLength(2048);
            entity.Property(e => e.FirstName).HasMaxLength(2048);
            entity.Property(e => e.LastName).HasMaxLength(2048);

            // EmailHash is a lookup digest (HMAC-SHA256 hex = 64 chars). It is NOT
            // a secret and NOT a rainbow-prone plain hash (peppered HMAC). Indexed
            // with TenantId for exact-match admin search on the encrypted email column.
            entity.Property(e => e.EmailHash).HasMaxLength(64);

            // Unique: one cache entry per user per tenant
            entity.HasIndex(e => new { e.TenantId, e.ExternalUserId })
                  .IsUnique()
                  .HasDatabaseName($"uq_{GranitIdentityDbProperties.DbTablePrefix}user_cache_tenant_external_id");

            // Exact-match search index — replaces the prior LIKE-scan over plaintext
            // (broken once Email became ciphertext).
            entity.HasIndex(e => new { e.TenantId, e.EmailHash })
                  .HasDatabaseName($"ix_{GranitIdentityDbProperties.DbTablePrefix}user_cache_tenant_email_hash");

            // NOTE: after encryption-at-rest was enabled the Username / Email / (LastName,FirstName)
            // indexes are dropped. LIKE-scans over encrypted columns would require
            // decrypting every row on every query — unusable on directories with 100k+
            // users. Free-text name search is no longer supported; the admin UI can
            // query by exact email instead.
        });

        return builder;
    }
}
