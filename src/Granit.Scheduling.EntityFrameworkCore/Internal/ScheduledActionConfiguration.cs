using Granit.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Scheduling.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="ScheduledAction"/>.
/// Table: <c>scheduling_scheduled_actions</c>.
/// </summary>
internal sealed class ScheduledActionConfiguration
    : IEntityTypeConfiguration<ScheduledAction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ScheduledAction> builder)
    {
        builder.ToTable(
            GranitSchedulingDbProperties.DbTablePrefix + "scheduled_actions",
            GranitSchedulingDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PayloadType)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .IsRequired();

        builder.Property(e => e.ExecuteAt)
            .IsRequired();

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(500);

        builder.Property(e => e.Status)
            .IsRequired();

        builder.Property(e => e.ExecutedAt);

        builder.Property(e => e.CancelledBy)
            .HasMaxLength(450);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(500);

        builder.Property(e => e.AttemptCount)
            .IsRequired();

        builder.HasIndex(e => new { e.Status, e.ExecuteAt })
            .HasDatabaseName($"ix_{GranitSchedulingDbProperties.DbTablePrefix}scheduled_actions_status_execute_at");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName($"ix_{GranitSchedulingDbProperties.DbTablePrefix}scheduled_actions_correlation_id");
    }
}
