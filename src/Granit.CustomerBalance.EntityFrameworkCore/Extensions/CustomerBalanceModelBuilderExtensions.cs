using Granit.CustomerBalance.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.CustomerBalance entity configurations.</summary>
public static class CustomerBalanceModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the CustomerBalance module.</summary>
    public static ModelBuilder ConfigureCustomerBalanceModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BalanceAccountConfiguration());
        modelBuilder.ApplyConfiguration(new BalanceTransactionConfiguration());
        return modelBuilder;
    }
}
