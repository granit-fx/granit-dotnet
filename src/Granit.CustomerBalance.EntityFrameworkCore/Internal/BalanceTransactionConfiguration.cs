using Granit.CustomerBalance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class BalanceTransactionConfiguration : IEntityTypeConfiguration<BalanceTransaction>
{
    public void Configure(EntityTypeBuilder<BalanceTransaction> builder)
    {
        builder.ToTable(GranitCustomerBalanceDbProperties.DbTablePrefix + "transactions", GranitCustomerBalanceDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Source).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ReferenceType).HasMaxLength(50);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.BalanceAccountId, e.CreatedAt })
            .HasDatabaseName($"ix_{GranitCustomerBalanceDbProperties.DbTablePrefix}transactions_account_created");

        builder.HasIndex(e => e.ExpiresAt)
            .HasFilter($"\"Source\" = {(int)TransactionSource.Promotional} AND \"Type\" = {(int)TransactionType.Credit}")
            .HasDatabaseName($"ix_{GranitCustomerBalanceDbProperties.DbTablePrefix}transactions_expires_at");

        // Dedupe stamp written by the daily expiration scanner once a CreditExpiringEto
        // has been emitted. Nullable — only populated for promotional credits that have
        // been alerted at least once.
        builder.Property(e => e.LastExpirationNotifiedAt);
    }
}
