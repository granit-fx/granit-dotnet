using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable(GranitPaymentsDbProperties.DbTablePrefix + "disputes", GranitPaymentsDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderDisputeId).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
    }
}
