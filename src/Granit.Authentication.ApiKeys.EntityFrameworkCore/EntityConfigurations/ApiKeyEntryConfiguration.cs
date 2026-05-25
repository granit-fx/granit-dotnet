using Granit.Authentication.ApiKeys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.EntityConfigurations;

/// <summary>
/// EF Core configuration for <see cref="ApiKeyEntry"/>.
/// </summary>
internal sealed class ApiKeyEntryConfiguration : IEntityTypeConfiguration<ApiKeyEntry>
{
    public void Configure(EntityTypeBuilder<ApiKeyEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitApiKeysDbProperties.DbTablePrefix + "entries",
            GranitApiKeysDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // Unique index on HashedKey for O(1) lookups
        builder.HasIndex(e => e.HashedKey)
            .IsUnique()
            .HasDatabaseName($"uq_{GranitApiKeysDbProperties.DbTablePrefix}entries_hashed_key");

        // Index on TenantId for multi-tenant queries
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName($"ix_{GranitApiKeysDbProperties.DbTablePrefix}entries_tenant_id");

        // Composite index supporting the daily "expiring soon" scanner —
        // covers the (RevokedAt IS NULL, ExpiresAt within window, LastExpirationNotifiedAt < dedupeBefore)
        // predicate. Order: ExpiresAt first to enable range seek, then LastExpirationNotifiedAt
        // for dedupe filtering, finally RevokedAt as the cheapest equality filter.
        builder.HasIndex(e => new { e.ExpiresAt, e.LastExpirationNotifiedAt, e.RevokedAt })
            .HasDatabaseName($"ix_{GranitApiKeysDbProperties.DbTablePrefix}entries_expiring_scan");

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.HashedKey).HasMaxLength(100).IsRequired(); // "vN$" prefix + SHA-256 hex (64 chars); 100 leaves headroom for future schemes
        builder.Property(e => e.Prefix).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastFourChars).HasMaxLength(4).IsRequired();
        builder.Property(e => e.Environment).HasMaxLength(10).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.ModifiedBy).HasMaxLength(200);
        builder.Property(e => e.DeletedBy).HasMaxLength(200);

        builder.Property(e => e.Type).HasMaxLength(20);
        builder.Property(e => e.CacheBehavior).HasMaxLength(20);
    }
}
