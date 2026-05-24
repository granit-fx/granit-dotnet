using Granit.BlobStorage.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.BlobStorage.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="BlobDescriptor"/>.
/// Table: <c>storage_blob_descriptors</c>.
/// </summary>
internal sealed class BlobDescriptorConfiguration : IEntityTypeConfiguration<BlobDescriptor>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlobDescriptor> builder)
    {
        builder.ToTable(
            GranitBlobStorageDbProperties.DbTablePrefix + "descriptors",
            GranitBlobStorageDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.ContainerName)
            .HasMaxLength(100)
            .IsRequired();

        // S3 object keys are up to 1 024 bytes per AWS specification.
        builder.Property(e => e.ObjectKey)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(e => e.OriginalFileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DeclaredContentType)
            .HasMaxLength(127)
            .IsRequired();

        builder.Property(e => e.VerifiedContentType)
            .HasMaxLength(127);

        builder.Property(e => e.MaxAllowedBytes)
            .IsRequired();

        builder.Property(e => e.SizeBytes);

        // Persisted as varchar by the Granit enum convention; explicit max length
        // keeps the original column width.
        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.ValidatedAt);

        builder.Property(e => e.DeletedAt);

        // ISO 27001 audit: rejection and deletion reasons are retained for 3 years.
        builder.Property(e => e.RejectionReason)
            .HasMaxLength(500);

        builder.Property(e => e.DeletionReason)
            .HasMaxLength(500);

        // Composite index for tenant-scoped queries by container.
        builder.HasIndex(e => new { e.TenantId, e.ContainerName })
            .HasDatabaseName($"ix_{GranitBlobStorageDbProperties.DbTablePrefix}descriptors_tenant_container");

        // The S3 object key is globally unique across all tenants.
        builder.HasIndex(e => e.ObjectKey)
            .IsUnique()
            .HasDatabaseName($"uq_{GranitBlobStorageDbProperties.DbTablePrefix}descriptors_object_key");
    }
}
