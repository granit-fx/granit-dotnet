using Granit.BackgroundJobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="BackgroundJobDefinition"/>.
/// Table: <c>background_jobs_background_jobs</c>.
/// </summary>
internal sealed class BackgroundJobDefinitionConfiguration
    : IEntityTypeConfiguration<BackgroundJobDefinition>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BackgroundJobDefinition> builder)
    {
        builder.ToTable(
            GranitBackgroundJobsDbProperties.DbTablePrefix + "background_jobs",
            GranitBackgroundJobsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.JobName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.MessageType)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.CronExpression)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.LastExecutedAt);

        builder.Property(e => e.NextExecutionAt);

        builder.Property(e => e.ConsecutiveFailureCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.TriggeredBy)
            .HasMaxLength(450);

        builder.HasIndex(e => e.JobName)
            .IsUnique()
            .HasDatabaseName($"uq_{GranitBackgroundJobsDbProperties.DbTablePrefix}background_jobs_name");
    }
}
