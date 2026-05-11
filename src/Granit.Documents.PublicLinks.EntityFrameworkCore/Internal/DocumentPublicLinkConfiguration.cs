using Granit.Documents.PublicLinks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentPublicLink"/>.
/// Table: <c>documents_public_links</c>.
/// </summary>
internal sealed class DocumentPublicLinkConfiguration : IEntityTypeConfiguration<DocumentPublicLink>
{
    /// <summary>Length, in bytes, of an HMAC-SHA256 digest — pinned for the <c>bytea</c> column.</summary>
    public const int TokenHashLength = 32;

    public void Configure(EntityTypeBuilder<DocumentPublicLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitDocumentsPublicLinksDbProperties.DbTablePrefix + "public_links",
            GranitDocumentsPublicLinksDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);
        builder.Property(e => e.DocumentId).IsRequired();

        builder.Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(TokenHashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(e => e.Scope)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.MaxUses);
        builder.Property(e => e.CurrentUses).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.Property(e => e.RevokedAt);
        builder.Property(e => e.RevokedBy);
        builder.Property(e => e.RevocationReason).HasMaxLength(500);

        // Anonymous lookup by token — must be unique across the entire table.
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName($"ux_{GranitDocumentsPublicLinksDbProperties.DbTablePrefix}public_links_token_hash");

        // Listing query (per-document, freshest-first via RevokedAt nulls-first).
        builder.HasIndex(e => new { e.DocumentId, e.RevokedAt })
            .HasDatabaseName($"ix_{GranitDocumentsPublicLinksDbProperties.DbTablePrefix}public_links_document");

        // Cleanup query: prune expired links per tenant.
        builder.HasIndex(e => new { e.TenantId, e.ExpiresAt })
            .HasDatabaseName($"ix_{GranitDocumentsPublicLinksDbProperties.DbTablePrefix}public_links_tenant_expires");
    }
}
