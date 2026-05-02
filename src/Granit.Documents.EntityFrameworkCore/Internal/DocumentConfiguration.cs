using Granit.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="Document"/>.
/// Table: <c>documents_documents</c>.
/// </summary>
internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitDocumentsDbProperties.DbTablePrefix + "documents",
            GranitDocumentsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.FolderId)
            .IsRequired();

        builder.Property(e => e.OwnerUserId)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(Document.MaxNameLength)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(Document.MaxDescriptionLength);

        builder.Property(e => e.CurrentVersionId);

        // Store enum as string for readability and resilience to reordering.
        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.TrashedAt);

        // Optimistic-concurrency token. uint maps to provider-native types
        // (PostgreSQL bigint via npgsql, SQL Server int, SQLite INTEGER); IsConcurrencyToken
        // makes EF compare it on UPDATE so concurrent writers surface as
        // DbUpdateConcurrencyException.
        builder.Property(e => e.RowVersion)
            .IsRequired()
            .IsConcurrencyToken();

        // FK back to documents_folders (no navigation property — the aggregate boundary
        // forbids cross-aggregate navigation, but the FK must exist for the planned
        // F8 cascade-trash + F2.4 path-prefix queries to remain efficient).
        builder.HasIndex(e => new { e.TenantId, e.FolderId })
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}documents_tenant_folder");

        // Active-status index used by the trash listing (F8.2) and the empty-trash job
        // (F9.2) to scan only the relevant rows.
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}documents_tenant_status");
    }
}
