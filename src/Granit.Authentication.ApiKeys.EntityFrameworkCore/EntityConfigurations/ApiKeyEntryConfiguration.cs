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
            GranitApiKeysDbProperties.DbTablePrefix + "api_keys",
            GranitApiKeysDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // Unique index on HashedKey for O(1) lookups
        builder.HasIndex(e => e.HashedKey)
            .IsUnique()
            .HasDatabaseName($"uq_{GranitApiKeysDbProperties.DbTablePrefix}api_keys_hashed_key");

        // Index on TenantId for multi-tenant queries
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName($"ix_{GranitApiKeysDbProperties.DbTablePrefix}api_keys_tenant_id");

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.HashedKey).HasMaxLength(64).IsRequired(); // SHA-256 hex = 64 chars
        builder.Property(e => e.Prefix).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastFourChars).HasMaxLength(4).IsRequired();
        builder.Property(e => e.Environment).HasMaxLength(10).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.ModifiedBy).HasMaxLength(200);
        builder.Property(e => e.DeletedBy).HasMaxLength(200);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CacheBehavior).HasConversion<string>().HasMaxLength(20);
    }
}
