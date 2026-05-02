using Granit.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="Folder"/>.
/// Table: <c>documents_folders</c>.
/// </summary>
internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitDocumentsDbProperties.DbTablePrefix + "folders",
            GranitDocumentsDbProperties.DbSchema,
            tb =>
            {
                // ParentFolderId is allowed to be NULL only for the tenant root. Column
                // identifiers are quoted to preserve PascalCase across providers (Postgres
                // lowercases unquoted identifiers; SQLite is case-insensitive but accepts quotes).
                tb.HasCheckConstraint(
                    $"ck_{GranitDocumentsDbProperties.DbTablePrefix}folders_parent_only_root_null",
                    $"\"{nameof(Folder.ParentFolderId)}\" IS NOT NULL OR \"{nameof(Folder.IsTenantRoot)}\" = TRUE");
            });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.ParentFolderId);

        builder.Property(e => e.Name)
            .HasMaxLength(Folder.MaxNameLength)
            .IsRequired();

        builder.Property(e => e.Path)
            .HasMaxLength(Folder.MaxPathLength)
            .IsRequired();

        builder.Property(e => e.Depth)
            .IsRequired();

        builder.Property(e => e.OwnerUserId)
            .IsRequired();

        builder.Property(e => e.IsTenantRoot)
            .IsRequired();

        // Store enum as string for readability and resilience to reordering.
        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.TrashedAt);

        // Exactly one tenant-root row per tenant — partial unique index. The filter
        // identifier is quoted to preserve PascalCase across providers.
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter($"\"{nameof(Folder.IsTenantRoot)}\" = TRUE")
            .HasDatabaseName($"ux_{GranitDocumentsDbProperties.DbTablePrefix}folders_one_root_per_tenant");

        // Sibling-name uniqueness inside a parent. Per-tenant scoping is implicit because
        // ParentFolderId is itself a tenant-scoped FK.
        builder.HasIndex(e => new { e.TenantId, e.ParentFolderId, e.Name })
            .IsUnique()
            .HasDatabaseName($"ux_{GranitDocumentsDbProperties.DbTablePrefix}folders_sibling_name");

        // Materialised-path index for ACL prefix scans (F6.2 will optionally promote this
        // to text_pattern_ops on PostgreSQL when wiring the effective-permission resolver).
        builder.HasIndex(e => new { e.TenantId, e.Path })
            .HasDatabaseName($"ix_{GranitDocumentsDbProperties.DbTablePrefix}folders_tenant_path");
    }
}
