using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Privacy.EntityFrameworkCore.DataExport.Internal;

internal sealed class ExportAssemblyCheckpointRowConfiguration : IEntityTypeConfiguration<ExportAssemblyCheckpointRow>
{
    public void Configure(EntityTypeBuilder<ExportAssemblyCheckpointRow> builder)
    {
        builder.ToTable(
            GranitPrivacyDbProperties.DbTablePrefix + "export_assembly_checkpoints",
            GranitPrivacyDbProperties.DbSchema);

        // (RequestId, TenantId) is the natural key — one in-flight assembly per
        // (request, tenant). No surrogate Id.
        builder.HasKey(e => new { e.RequestId, e.TenantId });

        builder.Property(e => e.LastCompletedShardIndex).IsRequired();
        builder.Property(e => e.NextFragmentIndex).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.Property(e => e.CompletedShardObjectKeys)
            .HasJsonConversion()
            .IsRequired();

        // ConcurrencyStamp is auto-wired by ApplyGranitConventions; pin the column shape.
        builder.Property(e => e.ConcurrencyStamp).IsRequired().HasMaxLength(36);
    }
}
