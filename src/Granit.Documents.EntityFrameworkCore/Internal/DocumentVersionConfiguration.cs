using Granit.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentVersion"/>.
/// Table: <c>documents_document_versions</c>.
/// </summary>
internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitDocumentsDbProperties.DbTablePrefix + "document_versions",
            GranitDocumentsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.DocumentId)
            .IsRequired();

        builder.Property(e => e.VersionNumber)
            .IsRequired();

        builder.Property(e => e.BlobDescriptorId)
            .IsRequired();

        builder.Property(e => e.SizeBytes)
            .IsRequired();

        builder.Property(e => e.ContentType)
            .HasMaxLength(127)
            .IsRequired();

        builder.Property(e => e.ContentHash)
            .HasMaxLength(128);

        builder.Property(e => e.UploadedByUserId)
            .IsRequired();

        builder.Property(e => e.UploadedAt)
            .IsRequired();

        builder.Property(e => e.CommitMessage)
            .HasMaxLength(DocumentVersion.MaxCommitMessageLength);

        // Monotonic-versioning invariant: at most one row per (DocumentId, VersionNumber)
        // tuple. F4.1's "VersionNumber = max + 1" computation relies on this so
        // concurrent re-uploads produce two distinct numbers, not the same number twice.
        builder.HasIndex(e => new { e.DocumentId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName($"ux_{GranitDocumentsDbProperties.DbTablePrefix}document_versions_doc_number");

        // Listing all versions of a document is the primary read path (F4.2).
        builder.HasIndex(e => new { e.DocumentId, e.UploadedAt })
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}document_versions_doc_uploaded");
    }
}
