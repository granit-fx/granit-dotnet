using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Timeline.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="Reaction"/>.
/// Table: <c>{prefix}reactions</c>.
/// </summary>
internal sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable(
            GranitTimelineDbProperties.DbTablePrefix + "reactions",
            GranitTimelineDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntryId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Emoji).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();

        // Cascade-delete with the parent TimelineEntry — reactions have no
        // meaning without their entry. Soft-delete is intentionally not
        // supported (a removed reaction is removed, not preserved for audit).
        builder.HasOne<TimelineEntry>()
            .WithMany()
            .HasForeignKey(x => x.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency guard — concurrent double-clicks collide here and
        // resolve to a single row. The toggle endpoint (story C2) catches the
        // unique-violation and treats it as "already added".
        builder.HasIndex(x => new { x.EntryId, x.UserId, x.Emoji })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitTimelineDbProperties.DbTablePrefix}reactions_unique");

        // Read all reactions on an entry (timeline stream enrichment, story C3).
        builder.HasIndex(x => new { x.EntryId, x.TenantId })
            .HasDatabaseName($"ix_{GranitTimelineDbProperties.DbTablePrefix}reactions_by_entry");
    }
}
