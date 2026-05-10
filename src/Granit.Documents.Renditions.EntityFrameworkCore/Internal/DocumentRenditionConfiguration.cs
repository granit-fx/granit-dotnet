using Granit.Documents.Renditions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentRendition"/>. Table:
/// <c>documents_renditions</c>.
/// </summary>
internal sealed class DocumentRenditionConfiguration : IEntityTypeConfiguration<DocumentRendition>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DocumentRendition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitRenditionsDbProperties.DbTablePrefix + "renditions",
            GranitRenditionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);
        builder.Property(e => e.DocumentId).IsRequired();
        builder.Property(e => e.DocumentVersionId).IsRequired();
        builder.Property(e => e.GeneratedFromVersionId).IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Format)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.BlobDescriptorId);
        builder.Property(e => e.SizeBytes);
        builder.Property(e => e.Width);
        builder.Property(e => e.Height);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CompletedAt);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(2048);

        // Triple-key uniqueness — one rendition row per (version, type, format) combination.
        // Re-runs after a failure UPSERT through the existing row.
        builder.HasIndex(e => new { e.DocumentVersionId, e.Type, e.Format })
            .IsUnique()
            .HasDatabaseName($"ux_{GranitRenditionsDbProperties.DbTablePrefix}renditions_version_type_format");

        // Fast lookup for "every rendition of this document" — drives the F16.3 list
        // endpoint and the cascading delete on permanent-delete.
        builder.HasIndex(e => e.DocumentId)
            .HasDatabaseName($"ix_{GranitRenditionsDbProperties.DbTablePrefix}renditions_document");
    }
}
