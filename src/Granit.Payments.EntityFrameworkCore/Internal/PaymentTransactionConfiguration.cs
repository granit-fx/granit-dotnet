using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable(GranitPaymentsDbProperties.DbTablePrefix + "transactions", GranitPaymentsDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.InvoiceId).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderTransactionId).HasMaxLength(256);
        builder.Property(e => e.ActionUrl).HasMaxLength(2048);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(e => e.FailureCode).HasMaxLength(100);
        builder.Property(e => e.FailureMessage).HasMaxLength(500);

        builder.HasMany(e => e.Refunds).WithOne().HasForeignKey("TransactionId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Disputes).WithOne().HasForeignKey("TransactionId").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName($"ix_{GranitPaymentsDbProperties.DbTablePrefix}transactions_tenant_status");
        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey }).IsUnique()
            .HasDatabaseName($"uq_{GranitPaymentsDbProperties.DbTablePrefix}transactions_tenant_idempotency");
        builder.HasIndex(e => new { e.ProviderName, e.ProviderTransactionId })
            .HasFilter("\"ProviderTransactionId\" IS NOT NULL")
            .HasDatabaseName($"ix_{GranitPaymentsDbProperties.DbTablePrefix}transactions_provider");
    }
}
