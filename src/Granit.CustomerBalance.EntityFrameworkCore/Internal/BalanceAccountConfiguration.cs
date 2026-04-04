using Granit.CustomerBalance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class BalanceAccountConfiguration : IEntityTypeConfiguration<BalanceAccount>
{
    public void Configure(EntityTypeBuilder<BalanceAccount> builder)
    {
        builder.ToTable(GranitCustomerBalanceDbProperties.DbTablePrefix + "accounts", GranitCustomerBalanceDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Balance).HasPrecision(18, 4).IsRequired();

        builder.HasMany(e => e.Transactions)
            .WithOne()
            .HasForeignKey(t => t.BalanceAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Currency })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitCustomerBalanceDbProperties.DbTablePrefix}accounts_tenant_currency");
    }
}
