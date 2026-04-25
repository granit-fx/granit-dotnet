using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Exports;
using Granit.CustomerBalance.Internal;
using Granit.CustomerBalance.Queries;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Invoicing;
using Granit.QueryEngine.Extensions;
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
        builder.Services.TryAddTransient<IAdminCreditService, DefaultAdminCreditService>();
        builder.Services.TryAddTransient<IAdminDebitService, DefaultAdminDebitService>();
        builder.Services.TryAddTransient<IOverpaymentCreditService, DefaultOverpaymentCreditService>();

        // Replace default pre-payment processor to deduct available credit before PSP charges.
        builder.Services.Replace(ServiceDescriptor
            .Scoped<IInvoicePrePaymentProcessor, CustomerBalancePrePaymentProcessor>());

        GranitActivitySourceRegistry.Register(CustomerBalanceActivitySource.Name);

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<BalanceAccount, BalanceAccountQueryDefinition>();
        builder.Services.AddQueryDefinition<BalanceTransaction, BalanceTransactionQueryDefinition>();
        builder.Services.AddExportDefinition<BalanceAccount, BalanceAccountExportDefinition>();
        builder.Services.AddExportDefinition<BalanceTransaction, BalanceTransactionExportDefinition>();

        return builder;
    }
}
