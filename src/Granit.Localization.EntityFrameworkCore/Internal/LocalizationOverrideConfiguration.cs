using Granit.Localization.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Localization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="LocalizationOverride"/>.
/// Table: <c>i18n_localization_overrides</c>.
/// </summary>
internal sealed class LocalizationOverrideConfiguration
    : IEntityTypeConfiguration<LocalizationOverride>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LocalizationOverride> builder)
    {
        builder.ToTable(
            GranitLocalizationDbProperties.DbTablePrefix + "overrides",
            GranitLocalizationDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.ResourceName)
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(e => e.CultureName)
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(e => e.Key)
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(e => e.Value)
               .HasMaxLength(4000)
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

        // Unique composite index: one override per (tenant, resource, culture, key)
        builder.HasIndex(e => new { e.TenantId, e.ResourceName, e.CultureName, e.Key })
               .IsUnique()
               .HasDatabaseName($"uq_{GranitLocalizationDbProperties.DbTablePrefix}overrides_tenant_resource_culture_key");

        // Non-unique index to speed up bulk reads per (tenant, resource, culture)
        builder.HasIndex(e => new { e.TenantId, e.ResourceName, e.CultureName })
               .HasDatabaseName($"ix_{GranitLocalizationDbProperties.DbTablePrefix}overrides_tenant_resource_culture");
    }
}
