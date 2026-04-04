using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class CustomerBalanceDbContext(
    DbContextOptions<CustomerBalanceDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<BalanceAccount> Accounts { get; set; } = null!;
    public DbSet<BalanceTransaction> Transactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureCustomerBalanceModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
