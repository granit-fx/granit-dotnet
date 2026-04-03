using Granit.Modularity;
using Granit.Payments.Mollie.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Payments.Mollie;

/// <summary>Mollie payment provider for Granit.Payments (EU-sovereign).</summary>
[DependsOn(typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsMollieModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IPaymentProvider, MolliePaymentProvider>();
        context.Services.AddSingleton<ICheckoutSessionFactory, MollieCheckoutSessionFactory>();
        context.Services.AddSingleton<IPaymentMethodManager, MolliePaymentMethodManager>();
        context.Services.AddSingleton<IPaymentWebhookVerifier, MollieWebhookVerifier>();
    }
}
