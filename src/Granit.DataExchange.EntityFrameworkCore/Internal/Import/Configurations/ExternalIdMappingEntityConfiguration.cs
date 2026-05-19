using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ExternalIdMappingEntity"/>.
/// </summary>
internal sealed class ExternalIdMappingEntityConfiguration : IEntityTypeConfiguration<ExternalIdMappingEntity>
{
    public void Configure(EntityTypeBuilder<ExternalIdMappingEntity> builder)
    {
        builder.ToTable(
            GranitDataExchangeDbProperties.DbTablePrefix + "external_id_mappings",
            GranitDataExchangeDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DefinitionName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(500).IsRequired();
        builder.Property(e => e.InternalId).IsRequired();
        builder.Property(e => e.TenantId);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.DefinitionName, e.ExternalId, e.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitDataExchangeDbProperties.DbTablePrefix}external_id_mappings_def_ext_tenant");
    }
}
