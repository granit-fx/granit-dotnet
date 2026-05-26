using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Indexing.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for <see cref="IndexedEntryRow{TKey}"/>.
/// </summary>
/// <remarks>
/// The tsvector generated column is wired by
/// <see cref="Extensions.ModelBuilderExtensions.HasGeneratedTsVectorColumn"/>
/// and is NOT configured here — keeping the dialect-specific concern out of the entity
/// configuration so the same DbContext type can be exercised against the EF Core
/// in-memory / SQLite providers in unit tests (no tsvector column).
/// </remarks>
internal sealed class IndexedEntryRowConfiguration<TKey> : IEntityTypeConfiguration<IndexedEntryRow<TKey>>
{
    public void Configure(EntityTypeBuilder<IndexedEntryRow<TKey>> builder)
    {
        builder.ToTable($"IndexedEntry_{typeof(TKey).Name}");

        builder.HasKey(e => new { e.TenantId, e.Key });

        builder.Property(e => e.Key).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.Language).HasMaxLength(16);
        builder.Property(e => e.Summary);
        builder.Property(e => e.Tags);

        // Hot-path lookup: GDPR Art. 17 handler deletes by (TenantId, DataSubjectId).
        builder.HasIndex(e => new { e.TenantId, e.DataSubjectId })
            .HasDatabaseName($"IX_IndexedEntry_{typeof(TKey).Name}_DataSubject");
    }
}
