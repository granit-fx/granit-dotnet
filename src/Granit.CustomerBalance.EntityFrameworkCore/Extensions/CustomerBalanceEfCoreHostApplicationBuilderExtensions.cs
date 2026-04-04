using Granit.CustomerBalance.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.CustomerBalance.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.CustomerBalance.</summary>
public static class CustomerBalanceEfCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit customer balance.</summary>
    public static IHostApplicationBuilder AddGranitCustomerBalanceEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<CustomerBalanceDbContext>(configure);
        builder.Services.AddInternalDbContextEnsurer<CustomerBalanceDbContext>();

        builder.Services.AddScoped<EfBalanceAccountStore>();
        builder.Services.TryAddScoped<IBalanceAccountReader>(sp => sp.GetRequiredService<EfBalanceAccountStore>());
        builder.Services.TryAddScoped<IBalanceAccountWriter>(sp => sp.GetRequiredService<EfBalanceAccountStore>());

        builder.Services.TryAddScoped<IBalanceTransactionReader, EfBalanceTransactionReader>();

        return builder;
    }
}
