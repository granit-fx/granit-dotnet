using Granit.Invoicing;
using Granit.Modularity;
using Granit.Payments.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Payments.Wolverine;

/// <summary>Wolverine integration for Granit.Payments.</summary>
[DependsOn(
    typeof(GranitPaymentsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitPaymentsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<IInvoicePrePaymentProcessor, PassThroughPrePaymentProcessor>();
        context.Services.TryAddScoped<WebhookProcessor>();
        context.Services.TryAddScoped<AutoChargeService>();
    }
}
