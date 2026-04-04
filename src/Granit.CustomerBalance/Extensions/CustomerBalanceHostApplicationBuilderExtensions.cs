using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Internal;
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
        builder.Services.TryAddTransient<ICreditExpirationService, DefaultCreditExpirationService>();
        builder.Services.TryAddTransient<IOverpaymentCreditService, DefaultOverpaymentCreditService>();
        GranitActivitySourceRegistry.Register(CustomerBalanceActivitySource.Name);

        return builder;
    }
}
