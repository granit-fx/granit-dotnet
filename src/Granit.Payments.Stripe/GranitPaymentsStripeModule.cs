using Granit.Modularity;
using Granit.Payments.Stripe.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Payments.Stripe;

/// <summary>Stripe payment provider for Granit.Payments.</summary>
[DependsOn(typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsStripeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
        context.Services.AddSingleton<ICheckoutSessionFactory, StripeCheckoutSessionFactory>();
        context.Services.AddSingleton<IPaymentMethodManager, StripePaymentMethodManager>();
        context.Services.AddSingleton<IPaymentWebhookVerifier, StripeWebhookVerifier>();
    }
}
