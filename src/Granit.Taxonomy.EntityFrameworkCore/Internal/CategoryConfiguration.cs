using Granit.Taxonomy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="Category"/>.
/// Table: <c>taxonomy_categories</c>.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitTaxonomyDbProperties.DbTablePrefix + "categories",
            GranitTaxonomyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);
        builder.Property(e => e.Scope)
            .HasMaxLength(Category.MaxScopeLength)
            .IsRequired();
        builder.Property(e => e.ParentId);
        builder.Property(e => e.Name)
            .HasMaxLength(Category.MaxNameLength)
            .IsRequired();
        builder.Property(e => e.Path)
            .HasMaxLength(Category.MaxPathLength)
            .IsRequired();
        builder.Property(e => e.Depth).IsRequired();
        builder.Property(e => e.IconName).HasMaxLength(Category.MaxIconNameLength);
        builder.Property(e => e.HideOnEntityCard).IsRequired();

        builder.Property(e => e.RowVersion)
            .IsRequired()
            .IsConcurrencyToken();

        // Sibling-name uniqueness within scope: two children of the same parent
        // cannot share a name (mirrors UX_documents_folders_sibling_name).
        builder.HasIndex(e => new { e.TenantId, e.Scope, e.ParentId, e.Name })
            .IsUnique()
            .HasDatabaseName(
                $"ux_{GranitTaxonomyDbProperties.DbTablePrefix}categories_sibling_name");

        // Subtree prefix scans (move re-materialisation, descendant queries).
        builder.HasIndex(e => new { e.TenantId, e.Path })
            .HasDatabaseName(
                $"ix_{GranitTaxonomyDbProperties.DbTablePrefix}categories_tenant_path");
    }
}
