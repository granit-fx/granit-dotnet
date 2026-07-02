using Granit.Privacy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;

internal sealed class DeletionRequestEntityConfiguration : IEntityTypeConfiguration<DeletionRequestEntity>
{
    public void Configure(EntityTypeBuilder<DeletionRequestEntity> builder)
    {
        builder.ToTable(
            GranitPrivacyDbProperties.DbTablePrefix + "deletion_requests",
            GranitPrivacyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.State)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.RequestedAt)
            .IsRequired();

        builder.Property(e => e.ScheduledDeletionAt)
            .IsRequired();

        builder.Property(e => e.Regulation)
            .HasMaxLength(40);

        // Comma-joined provider names that never acknowledged erasure (PartiallyExecuted only).
        // Bounded generously — provider names are short and the list is a handful at most.
        builder.Property(e => e.MissingProviders)
            .HasMaxLength(1000);

        // User timeline lookup (GET /privacy/erasure → GetByUserAsync).
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.RequestedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}deletion_requests_user_timeline");

        // Scheduler scan for expired deferrals.
        builder.HasIndex(e => new { e.State, e.ScheduledDeletionAt })
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}deletion_requests_due");
    }
}
