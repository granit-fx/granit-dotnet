using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Timeline.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TimelineEntry"/>.
/// Table: <c>timeline_entries</c>.
/// </summary>
internal sealed class TimelineEntryConfiguration : IEntityTypeConfiguration<TimelineEntry>
{
    public void Configure(EntityTypeBuilder<TimelineEntry> builder)
    {
        builder.ToTable(
            GranitTimelineDbProperties.DbTablePrefix + "entries",
            GranitTimelineDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EntryType).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(65_536).IsRequired();
        builder.Property(x => x.AuthorId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.AuthorName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        // Primary stream query: all entries for an entity, newest first
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.TenantId, x.CreatedAt })
            .IsDescending(false, false, false, true)
            .HasDatabaseName($"ix_{GranitTimelineDbProperties.DbTablePrefix}entries_entity_stream");

        // Self-referencing for threaded replies (no navigation property)
        builder.HasIndex(x => x.ParentEntryId)
            .HasDatabaseName($"ix_{GranitTimelineDbProperties.DbTablePrefix}entries_parent");
    }
}
