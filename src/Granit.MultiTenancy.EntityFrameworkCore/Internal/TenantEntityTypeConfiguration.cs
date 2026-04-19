using Granit.MultiTenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity configuration for <see cref="Tenant"/>.
/// Table: <c>{prefix}tenants</c> with a unique index on <c>Identifier</c>.
/// </summary>
internal sealed class TenantEntityTypeConfiguration : IEntityTypeConfiguration<Tenant>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable(
            MultiTenancyDbProperties.DbTablePrefix + "tenants",
            MultiTenancyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(e => e.Identifier)
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(e => e.ContactEmail)
               .HasMaxLength(256);

        builder.Property(e => e.Jurisdiction)
               .HasMaxLength(16);

        builder.Property(e => e.Activated)
               .IsRequired();

        builder.Property(e => e.CustomDomain)
               .HasMaxLength(253);

        // Audit columns (FullAuditedAggregateRoot)
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(450).IsRequired();
        builder.Property(e => e.ModifiedAt);
        builder.Property(e => e.ModifiedBy).HasMaxLength(450);
        builder.Property(e => e.IsDeleted).IsRequired();
        builder.Property(e => e.DeletedAt);
        builder.Property(e => e.DeletedBy).HasMaxLength(450);

        // Unique index on Identifier for fast lookup by slug/subdomain
        builder.HasIndex(e => e.Identifier)
               .IsUnique()
               .HasDatabaseName("uq_tenants_identifier");

        // Unique filtered index on CustomDomain (non-null only) for inbound resolution
        builder.HasIndex(e => e.CustomDomain)
               .IsUnique()
               .HasFilter("\"CustomDomain\" IS NOT NULL")
               .HasDatabaseName("uq_tenants_custom_domain");
    }
}
