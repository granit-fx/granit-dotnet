using Granit.Taxonomy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="CategoryAssignment"/>.
/// Table: <c>taxonomy_category_assignments</c>.
/// </summary>
internal sealed class CategoryAssignmentConfiguration : IEntityTypeConfiguration<CategoryAssignment>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CategoryAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitTaxonomyDbProperties.DbTablePrefix + "category_assignments",
            GranitTaxonomyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);
        builder.Property(e => e.CategoryId).IsRequired();
        builder.Property(e => e.TargetType)
            .HasMaxLength(CategoryAssignment.MaxTargetTypeLength)
            .IsRequired();
        builder.Property(e => e.TargetId).IsRequired();
        builder.Property(e => e.AssignedAt).IsRequired();
        builder.Property(e => e.AssignedByUserId).IsRequired();

        // Single-category-per-target invariant (ADR-054, opposite of TagAssignment).
        builder.HasIndex(e => new { e.TenantId, e.TargetType, e.TargetId })
            .IsUnique()
            .HasDatabaseName(
                $"ux_{GranitTaxonomyDbProperties.DbTablePrefix}category_assignments_tenant_target");

        // "Things classified under category X" lookup.
        builder.HasIndex(e => new { e.TenantId, e.CategoryId })
            .HasDatabaseName(
                $"ix_{GranitTaxonomyDbProperties.DbTablePrefix}category_assignments_tenant_category");
    }
}
