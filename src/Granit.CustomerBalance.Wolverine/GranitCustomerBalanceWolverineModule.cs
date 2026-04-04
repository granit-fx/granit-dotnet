using Granit.CustomerBalance.Wolverine.Internal;
using Granit.Invoicing;
using Granit.Modularity;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.CustomerBalance.Wolverine;

/// <summary>
/// Wolverine integration for Granit.CustomerBalance.
/// Replaces the default <see cref="IInvoicePrePaymentProcessor"/> with one that
/// deducts available credit before the PSP charge.
/// </summary>
[DependsOn(
    typeof(GranitCustomerBalanceModule),
    typeof(GranitInvoicingModule),
    typeof(GranitWolverineModule))]
public sealed class GranitCustomerBalanceWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.Replace(ServiceDescriptor
            .Scoped<IInvoicePrePaymentProcessor, CustomerBalancePrePaymentProcessor>());
}
