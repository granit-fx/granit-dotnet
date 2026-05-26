using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Indexing.EntityFrameworkCore.Configurations;

/// <summary>EF Core configuration for <see cref="IndexingRebuildCheckpointRow"/>.</summary>
internal sealed class IndexingRebuildCheckpointRowConfiguration : IEntityTypeConfiguration<IndexingRebuildCheckpointRow>
{
    public void Configure(EntityTypeBuilder<IndexingRebuildCheckpointRow> builder)
    {
        builder.ToTable("IndexingRebuildCheckpoint");

        // (TenantId, SourceName) is the natural key. We deliberately don't add a
        // surrogate Id — checkpoints are upsert-by-tuple.
        builder.HasKey(e => new { e.TenantId, e.SourceName });

        builder.Property(e => e.SourceName).IsRequired().HasMaxLength(128);
        builder.Property(e => e.LastProcessedKey).IsRequired().HasMaxLength(256);
    }
}
