using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable(GranitPaymentsDbProperties.DbTablePrefix + "refunds", GranitPaymentsDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ProviderRefundId).HasMaxLength(256);
        builder.Property(e => e.Reason).HasMaxLength(500);
    }
}
