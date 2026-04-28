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

        // PartyId is a SingleValueObject<Guid> — declared as a scalar property so EF Core
        // does not discover it as a navigation. Value converter applied by ApplyGranitConventions.
        builder.Property(e => e.PartyId).IsRequired();

        builder.HasMany(e => e.Transactions)
            .WithOne()
            .HasForeignKey(t => t.BalanceAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uniqueness is now per-(party, currency) — a tenant can hold many balances,
        // one per (party, currency) pair, matching real e-commerce / multi-buyer flows.
        builder.HasIndex(e => new { e.PartyId, e.Currency })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitCustomerBalanceDbProperties.DbTablePrefix}accounts_party_currency");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName($"ix_{GranitCustomerBalanceDbProperties.DbTablePrefix}accounts_tenant");
    }
}
