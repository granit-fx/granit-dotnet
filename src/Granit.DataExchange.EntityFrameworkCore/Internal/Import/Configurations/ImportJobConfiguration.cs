using Granit.DataExchange.Import.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ImportJob"/>.
/// </summary>
internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable(
            GranitDataExchangeDbProperties.DbTablePrefix + "import_jobs",
            GranitDataExchangeDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EntityTypeName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.OriginalFileName).HasMaxLength(500).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(127).IsRequired();
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.BlobReference).HasMaxLength(500).IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.MappingsJson);
        builder.Property(e => e.ReportJson);
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.TenantId);

        // Audit trail (ISO 27001)
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ModifiedAt);
        builder.Property(e => e.ModifiedBy).HasMaxLength(200);

        builder.HasIndex(e => new { e.DefinitionName, e.Status })
            .HasDatabaseName($"ix_{GranitDataExchangeDbProperties.DbTablePrefix}import_jobs_definition_status");
    }
}
