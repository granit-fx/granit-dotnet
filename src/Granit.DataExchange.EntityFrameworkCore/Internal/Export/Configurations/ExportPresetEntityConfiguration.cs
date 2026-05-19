using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ExportPresetEntity"/>.
/// </summary>
internal sealed class ExportPresetEntityConfiguration : IEntityTypeConfiguration<ExportPresetEntity>
{
    public void Configure(EntityTypeBuilder<ExportPresetEntity> builder)
    {
        builder.ToTable(
            GranitDataExchangeDbProperties.DbTablePrefix + "export_presets",
            GranitDataExchangeDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PresetName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TenantId);
        builder.Property(e => e.FieldsJson).IsRequired();
        builder.Property(e => e.Format).HasMaxLength(10).IsRequired();
        builder.Property(e => e.IncludeIdForImport).IsRequired();
        builder.Property(e => e.SavedAt).IsRequired();
        builder.Property(e => e.SavedBy).HasMaxLength(200).IsRequired();

        builder.HasIndex(e => new { e.DefinitionName, e.PresetName, e.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitDataExchangeDbProperties.DbTablePrefix}export_presets_def_name_tenant");
    }
}
