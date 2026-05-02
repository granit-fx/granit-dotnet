using Granit.Activities.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Activities.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="Activity"/>.
/// Table: <c>activities_activities</c> (or <c>{prefix}activities</c>).
/// </summary>
internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable(
            GranitActivitiesDbProperties.DbTablePrefix + "activities",
            GranitActivitiesDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EntityId).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AssignedToUserId).IsRequired();
        builder.Property(x => x.DueAt).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2_000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        // Per-entity activities panel ("show all activities on this Party").
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.TenantId, x.DueAt })
            .HasDatabaseName($"ix_{GranitActivitiesDbProperties.DbTablePrefix}activities_entity");

        // "My open activities" inbox query — assignee + status + due date.
        builder.HasIndex(x => new { x.TenantId, x.AssignedToUserId, x.Status, x.DueAt })
            .HasDatabaseName($"ix_{GranitActivitiesDbProperties.DbTablePrefix}activities_inbox");

        // Overdue scan (BG job, story A8) — narrow over (Status, DueAt) so the
        // partial-table scan stays cheap regardless of total row count.
        builder.HasIndex(x => new { x.TenantId, x.Status, x.DueAt })
            .HasDatabaseName($"ix_{GranitActivitiesDbProperties.DbTablePrefix}activities_overdue_scan");
    }
}
