using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Configurations;

/// <summary>
/// EF Core configuration for <see cref="SavedMappingEntity"/>.
/// </summary>
internal sealed class SavedMappingEntityConfiguration : IEntityTypeConfiguration<SavedMappingEntity>
{
    public void Configure(EntityTypeBuilder<SavedMappingEntity> builder)
    {
        builder.ToTable(
            GranitDataExchangeDbProperties.DbTablePrefix + "saved_mappings",
            GranitDataExchangeDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TenantId);
        builder.OwnsMany(e => e.Mappings, m =>
        {
            m.ToJson();
            m.Property(p => p.SourceColumn);
            m.Property(p => p.TargetProperty);
            m.Property(p => p.Confidence);
        });
        builder.Property(e => e.SavedAt).IsRequired();
        builder.Property(e => e.SavedBy).HasMaxLength(200).IsRequired();

        builder.HasIndex(e => new { e.DefinitionName, e.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitDataExchangeDbProperties.DbTablePrefix}saved_mappings_def_tenant");
    }
}
