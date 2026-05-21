using Granit.DataExchange.Export.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ExportJob"/>.
/// </summary>
internal sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable(
            GranitDataExchangeDbProperties.DbTablePrefix + "export_jobs",
            GranitDataExchangeDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Format).HasMaxLength(10).IsRequired();
        // ExportRequest is serialized as an opaque JSON string via the framework helper
        // rather than .ToJson() owned mapping: ExportRequest's IReadOnlyDictionary and
        // IReadOnlyList interface-typed properties don't compose cleanly with EF Core 10
        // owned-types, while System.Text.Json handles them transparently.
        builder.Property(e => e.Request).HasJsonConversion().IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.BlobReference).HasMaxLength(500);
        builder.Property(e => e.FileName).HasMaxLength(500);
        builder.Property(e => e.RowCount);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.TenantId);

        // Audit fields from AuditedEntity
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.ModifiedAt);
        builder.Property(e => e.ModifiedBy).HasMaxLength(200);

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName($"ix_{GranitDataExchangeDbProperties.DbTablePrefix}export_jobs_tenant_status");

        builder.HasIndex(e => new { e.DefinitionName, e.Status })
            .HasDatabaseName($"ix_{GranitDataExchangeDbProperties.DbTablePrefix}export_jobs_definition_status");
    }
}
