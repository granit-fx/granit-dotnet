using Granit.Taxonomy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TagAssignment"/>.
/// Table: <c>taxonomy_tag_assignments</c>.
/// </summary>
internal sealed class TagAssignmentConfiguration : IEntityTypeConfiguration<TagAssignment>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TagAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitTaxonomyDbProperties.DbTablePrefix + "tag_assignments",
            GranitTaxonomyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);
        builder.Property(e => e.TagId).IsRequired();

        builder.Property(e => e.TargetType)
            .HasMaxLength(TagAssignment.MaxTargetTypeLength)
            .IsRequired();

        builder.Property(e => e.TargetId).IsRequired();
        builder.Property(e => e.AssignedAt).IsRequired();
        builder.Property(e => e.AssignedByUserId).IsRequired();

        // ADR-054 invariant: a tag is assigned to a target at most once per tenant.
        builder.HasIndex(e => new { e.TenantId, e.TagId, e.TargetType, e.TargetId })
            .IsUnique()
            .HasDatabaseName(
                $"ux_{GranitTaxonomyDbProperties.DbTablePrefix}tag_assignments_tenant_tag_target");

        // "Tags of entity X" lookup — used by the per-target list endpoint and the
        // T5.1 cleanup handler when the target is hard-deleted.
        builder.HasIndex(e => new { e.TenantId, e.TargetType, e.TargetId })
            .HasDatabaseName(
                $"ix_{GranitTaxonomyDbProperties.DbTablePrefix}tag_assignments_tenant_target");

        // "Things tagged X" lookup — used by the cross-entity search endpoint (T3.1)
        // and orphan-cleanup when a tag is deleted.
        builder.HasIndex(e => new { e.TenantId, e.TagId })
            .HasDatabaseName(
                $"ix_{GranitTaxonomyDbProperties.DbTablePrefix}tag_assignments_tenant_tag");
    }
}
