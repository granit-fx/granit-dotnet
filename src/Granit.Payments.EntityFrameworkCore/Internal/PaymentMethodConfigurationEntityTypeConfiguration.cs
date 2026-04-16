using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class PaymentMethodConfigurationEntityTypeConfiguration
    : IEntityTypeConfiguration<PaymentMethodConfiguration>
{
    public void Configure(EntityTypeBuilder<PaymentMethodConfiguration> builder)
    {
        builder.ToTable(
            GranitPaymentsDbProperties.DbTablePrefix + "payment_method_configurations",
            GranitPaymentsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MethodType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DisplayLabel).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Category).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(e => new { e.ProviderName, e.MethodType })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitPaymentsDbProperties.DbTablePrefix}method_configs_provider_method");
    }
}
