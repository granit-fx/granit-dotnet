using Granit.DataExchange.Import.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
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
            .HasMaxLength(20);

        builder.OwnsMany(e => e.Mappings, m =>
        {
            m.ToJson();
            m.Property(p => p.SourceColumn);
            m.Property(p => p.TargetProperty);
            m.Property(p => p.Confidence);
        });

        // ImportReport is serialized as an opaque JSON string via the framework helper
        // rather than .ToJson() owned mapping: ImportReport's nested IReadOnlyList<ImportRowError>
        // + TimeSpan + enum compose awkwardly under EF Core 10 owned-types, while
        // round-tripping through System.Text.Json is straightforward.
        builder.Property(e => e.Report).HasJsonConversion();
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
