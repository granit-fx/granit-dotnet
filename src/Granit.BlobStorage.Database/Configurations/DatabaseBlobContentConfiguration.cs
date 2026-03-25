using Granit.BlobStorage.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.BlobStorage.Database.Configurations;

/// <summary>
/// EF Core configuration for <see cref="DatabaseBlobContent"/>.
/// </summary>
internal sealed class DatabaseBlobContentConfiguration : IEntityTypeConfiguration<DatabaseBlobContent>
{
    public void Configure(EntityTypeBuilder<DatabaseBlobContent> builder)
    {
        builder.ToTable("storage_blob_contents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.ObjectKey)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.ObjectKey)
            .IsUnique()
            .HasDatabaseName("uq_storage_blob_contents_object_key");

        builder.HasIndex(e => new { e.TenantId, e.ObjectKey })
            .HasDatabaseName("ix_storage_blob_contents_tenant_object_key");
    }
}
