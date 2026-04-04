using Granit.CustomerBalance.Diagnostics;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.CustomerBalance.Extensions;

/// <summary>
/// Extension methods for registering the Granit customer balance infrastructure.
/// </summary>
public static class CustomerBalanceHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit customer balance infrastructure.
    /// </summary>
    public static IHostApplicationBuilder AddGranitCustomerBalance(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<CustomerBalanceMetrics>();
        GranitActivitySourceRegistry.Register(CustomerBalanceActivitySource.Name);

        return builder;
    }
}
