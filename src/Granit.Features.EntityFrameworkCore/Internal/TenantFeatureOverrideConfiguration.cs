using Granit.Features.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Features.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TenantFeatureOverride"/>.
/// Table: <c>feature_overrides</c>.
/// </summary>
internal sealed class TenantFeatureOverrideConfiguration
    : IEntityTypeConfiguration<TenantFeatureOverride>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantFeatureOverride> builder)
    {
        builder.ToTable(
            GranitFeaturesDbProperties.DbTablePrefix + "overrides",
            GranitFeaturesDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // Nullable: null = global scope (no specific tenant), non-null = tenant-scoped override.
        // IFeatureStoreWriter.SetAsync(featureName, tenantId: null) stores a global override
        // that applies when no tenant context is active.
        builder.Property(e => e.TenantId);

        builder.Property(e => e.FeatureName)
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(e => e.Value)
               .HasMaxLength(2000)
               .IsRequired();

        // ISO 27001 audit columns — populated by AuditedEntityInterceptor
        builder.Property(e => e.CreatedAt)
               .IsRequired();

        builder.Property(e => e.CreatedBy)
               .HasMaxLength(450)
               .IsRequired();

        builder.Property(e => e.ModifiedAt);

        builder.Property(e => e.ModifiedBy)
               .HasMaxLength(450);

        // Unique composite index: one override per (tenant, feature)
        builder.HasIndex(e => new { e.TenantId, e.FeatureName })
               .IsUnique()
               .HasDatabaseName($"uq_{GranitFeaturesDbProperties.DbTablePrefix}overrides_tenant_feature");
    }
}
