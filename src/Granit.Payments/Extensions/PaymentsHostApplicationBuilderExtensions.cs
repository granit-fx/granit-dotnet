using Granit.Diagnostics;
using Granit.Invoicing;
using Granit.Payments.Diagnostics;
using Granit.Payments.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Payments.Extensions;

/// <summary>Extension methods for registering the Granit payments infrastructure.</summary>
public static class PaymentsHostApplicationBuilderExtensions
{
    /// <summary>Adds the Granit payments infrastructure.</summary>
    public static IHostApplicationBuilder AddGranitPayments(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<PaymentsMetrics>();
        builder.Services.TryAddTransient<IAutoChargeService, DefaultAutoChargeService>();
        builder.Services.TryAddScoped<IInvoicePrePaymentProcessor, PassThroughPrePaymentProcessor>();
        builder.Services.TryAddTransient<IWebhookProcessor, DefaultWebhookProcessor>();
        GranitActivitySourceRegistry.Register(PaymentsActivitySource.Name);

        builder.Services.TryAddScoped<IPaymentProviderResolver, DefaultPaymentProviderResolver>();

        return builder;
    }
}
