using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TemplateRevisionEntity"/>.
/// </summary>
internal sealed class TemplateRevisionEntityConfiguration
    : IEntityTypeConfiguration<TemplateRevisionEntity>
{
    public void Configure(EntityTypeBuilder<TemplateRevisionEntity> builder)
    {
        builder.ToTable(
            GranitTemplatingDbProperties.DbTablePrefix + "revisions",
            GranitTemplatingDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.RevisionId);

        builder.Property(e => e.TemplateName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Culture).HasMaxLength(10);
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(127).IsRequired();

        builder.Property(e => e.LifecycleStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.IsPublished).IsRequired();
        builder.Property(e => e.VersionId).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.PublishedAt);
        builder.Property(e => e.PublishedBy).HasMaxLength(200);
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.ModifiedBy).HasMaxLength(200);

        builder.HasIndex(e => new { e.TemplateName, e.Culture, e.LifecycleStatus })
            .HasDatabaseName($"ix_{GranitTemplatingDbProperties.DbTablePrefix}revisions_name_culture_status");

        builder.HasIndex(e => new { e.TemplateName, e.Culture })
            .HasDatabaseName($"ix_{GranitTemplatingDbProperties.DbTablePrefix}revisions_name_culture");

        builder.HasIndex(e => new { e.TenantId, e.TemplateName, e.Culture })
            .HasFilter("\"IsPublished\" = true")
            .IsUnique()
            .HasDatabaseName($"uq_{GranitTemplatingDbProperties.DbTablePrefix}revisions_published");

        builder.HasIndex(e => new { e.VersionId, e.Version })
            .HasDatabaseName($"ix_{GranitTemplatingDbProperties.DbTablePrefix}revisions_version");

        builder.Property(e => e.CategoryId);
        builder.HasOne<TemplateCategoryEntity>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
