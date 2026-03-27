using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Timeline.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TimelineAttachment"/>.
/// Table: <c>timeline_attachments</c>.
/// </summary>
internal sealed class TimelineAttachmentConfiguration : IEntityTypeConfiguration<TimelineAttachment>
{
    public void Configure(EntityTypeBuilder<TimelineAttachment> builder)
    {
        builder.ToTable(
            GranitTimelineDbProperties.DbTablePrefix + "attachments",
            GranitTimelineDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();

        // Lookup attachments for a given entry (tenant-partitioned)
        builder.HasIndex(x => new { x.EntryId, x.TenantId })
            .HasDatabaseName($"ix_{GranitTimelineDbProperties.DbTablePrefix}attachments_entry");
    }
}
