using Granit.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentShare"/>.
/// Table: <c>documents_shares</c>.
/// </summary>
/// <remarks>
/// Implements ADR-052 §Permission resolution model: a CHECK constraint pins the
/// "exactly one of FolderId / DocumentId is non-null" invariant at the database level,
/// and two filtered indexes back the F6.2 effective-permission resolver query.
/// </remarks>
internal sealed class DocumentShareConfiguration : IEntityTypeConfiguration<DocumentShare>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DocumentShare> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitDocumentsDbProperties.DbTablePrefix + "shares",
            GranitDocumentsDbProperties.DbSchema,
            tb =>
            {
                // Per ADR-052: exactly one of FolderId / DocumentId is populated, matching
                // the TargetType discriminator. Stored as a string column (HasConversion<string>
                // below) so the literals 'Folder' / 'Document' are stable across providers.
                // Identifiers are quoted to preserve PascalCase across providers (Postgres
                // lowercases unquoted identifiers; SQLite is case-insensitive but accepts quotes).
                tb.HasCheckConstraint(
                    $"ck_{GranitDocumentsDbProperties.DbTablePrefix}shares_target_exactly_one",
                    $"(\"{nameof(DocumentShare.TargetType)}\" = 'Folder'   AND \"{nameof(DocumentShare.FolderId)}\"   IS NOT NULL AND \"{nameof(DocumentShare.DocumentId)}\" IS NULL)"
                    + $" OR (\"{nameof(DocumentShare.TargetType)}\" = 'Document' AND \"{nameof(DocumentShare.DocumentId)}\" IS NOT NULL AND \"{nameof(DocumentShare.FolderId)}\"   IS NULL)");
            });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.TargetType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.FolderId);
        builder.Property(e => e.DocumentId);

        builder.Property(e => e.GranteeType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.GranteeId)
            .IsRequired();

        builder.Property(e => e.Permission)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.Property(e => e.ExpiresAt);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .IsRequired();

        // F6.2 resolver lookup paths — filtered indexes so each row appears in exactly one
        // of the two structures (matches the CHECK partitioning above). Filter literals are
        // quoted to preserve PascalCase identifiers; the value 'Folder' / 'Document' matches
        // the string conversion configured above.
        builder.HasIndex(e => new { e.TenantId, e.GranteeId, e.FolderId })
            .HasFilter($"\"{nameof(DocumentShare.TargetType)}\" = 'Folder'")
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}shares_grantee_folder");

        builder.HasIndex(e => new { e.TenantId, e.GranteeId, e.DocumentId })
            .HasFilter($"\"{nameof(DocumentShare.TargetType)}\" = 'Document'")
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}shares_grantee_document");

        // Listing endpoints (GET /folders/{id}/shares, GET /documents/{id}/shares) sort by
        // CreatedAt — the supporting index also covers tenant-scoped pagination cheaply.
        builder.HasIndex(e => new { e.TenantId, e.FolderId, e.CreatedAt })
            .HasFilter($"\"{nameof(DocumentShare.TargetType)}\" = 'Folder'")
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}shares_folder_listing");

        builder.HasIndex(e => new { e.TenantId, e.DocumentId, e.CreatedAt })
            .HasFilter($"\"{nameof(DocumentShare.TargetType)}\" = 'Document'")
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}shares_document_listing");
    }
}
