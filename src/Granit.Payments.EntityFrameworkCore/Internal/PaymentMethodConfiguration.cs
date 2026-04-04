using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable(GranitPaymentsDbProperties.DbTablePrefix + "payment_methods", GranitPaymentsDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderMethodId).HasMaxLength(256).IsRequired();
        builder.Property(e => e.DisplayLabel).HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsDefault).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => new { e.TenantId, e.IsDefault })
            .HasDatabaseName($"ix_{GranitPaymentsDbProperties.DbTablePrefix}methods_tenant_default");
    }
}
