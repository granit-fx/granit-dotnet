using Granit.Settings.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Settings.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="SettingRecord"/>.
/// Table: <c>settings_setting_records</c>.
/// </summary>
internal sealed class SettingRecordConfiguration : IEntityTypeConfiguration<SettingRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettingRecord> builder)
    {
        builder.ToTable(
            GranitSettingsDbProperties.DbTablePrefix + "setting_records",
            GranitSettingsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(e => e.ProviderName)
               .HasMaxLength(4)
               .IsRequired();

        builder.Property(e => e.ProviderKey)
               .HasMaxLength(256);

        builder.Property(e => e.Value);

        // ISO 27001 audit columns — populated automatically by AuditedEntityInterceptor
        builder.Property(e => e.CreatedAt)
               .IsRequired();

        builder.Property(e => e.CreatedBy)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(e => e.ModifiedAt);

        builder.Property(e => e.ModifiedBy)
               .HasMaxLength(256);

        // Unique composite index: one record per (Name, ProviderName, ProviderKey)
        builder.HasIndex(e => new { e.Name, e.ProviderName, e.ProviderKey })
               .IsUnique()
               .HasDatabaseName($"uq_{GranitSettingsDbProperties.DbTablePrefix}setting_records_name_provider");
    }
}
