using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class ProviderCustomerMappingConfiguration : IEntityTypeConfiguration<ProviderCustomerMapping>
{
    public void Configure(EntityTypeBuilder<ProviderCustomerMapping> builder)
    {
        builder.ToTable(
            GranitPaymentsDbProperties.DbTablePrefix + "provider_customer_mappings",
            GranitPaymentsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ProviderCustomerId).HasMaxLength(256).IsRequired();

        builder.HasIndex(e => new { e.ProviderName, e.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitPaymentsDbProperties.DbTablePrefix}customer_mappings_provider_tenant");
    }
}
