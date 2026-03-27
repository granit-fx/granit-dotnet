using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TemplateCategoryEntity"/>.
/// Table: <c>templating_categories</c>.
/// </summary>
internal sealed class TemplateCategoryEntityConfiguration
    : IEntityTypeConfiguration<TemplateCategoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TemplateCategoryEntity> builder)
    {
        builder.ToTable(
            GranitTemplatingDbProperties.DbTablePrefix + "categories",
            GranitTemplatingDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.Icon)
            .HasMaxLength(100);

        builder.Property(e => e.SortOrder)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(200)
            .IsRequired();

        // Unique constraint on (TenantId, Name) for multi-tenant business rule enforcement.
        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitTemplatingDbProperties.DbTablePrefix}categories_tenant_name");

        // Sort index for default ordering (SortOrder, Name).
        builder.HasIndex(e => new { e.SortOrder, e.Name })
            .HasDatabaseName($"ix_{GranitTemplatingDbProperties.DbTablePrefix}categories_sort");
    }
}
