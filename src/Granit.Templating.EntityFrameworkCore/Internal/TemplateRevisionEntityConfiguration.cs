using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TemplateRevisionEntity"/>.
/// Table: <c>templating_revisions</c>.
/// </summary>
internal sealed class TemplateRevisionEntityConfiguration
    : IEntityTypeConfiguration<TemplateRevisionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TemplateRevisionEntity> builder)
    {
        builder.ToTable(
            GranitTemplatingDbProperties.DbTablePrefix + "revisions",
            GranitTemplatingDbProperties.DbSchema);

        builder.HasKey(e => e.RevisionId);

        builder.Property(e => e.TemplateName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Culture)
            .HasMaxLength(10);

        // Content is unbounded — stored as TEXT in PostgreSQL, nvarchar(max) in SQL Server
        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.MimeType)
            .HasMaxLength(127)
            .IsRequired();

        // Store enum as string for readability and resilience to member reordering.
        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.PublishedAt);

        builder.Property(e => e.PublishedBy)
            .HasMaxLength(200);

        //ISO 27001: archival metadata retained for 3-year audit trail.
        builder.Property(e => e.ArchivedAt);

        builder.Property(e => e.ArchivedBy)
            .HasMaxLength(200);

        // Composite index for lifecycle queries: find draft/published by (name, culture, status)
        builder.HasIndex(e => new { e.TemplateName, e.Culture, e.Status })
            .HasDatabaseName($"ix_{GranitTemplatingDbProperties.DbTablePrefix}revisions_name_culture_status");

        // Index for history queries: all revisions for a given key
        builder.HasIndex(e => new { e.TemplateName, e.Culture })
            .HasDatabaseName($"ix_{GranitTemplatingDbProperties.DbTablePrefix}revisions_name_culture");

        // Optional FK to template category.
        builder.Property(e => e.CategoryId);

        builder.HasOne<TemplateCategoryEntity>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
