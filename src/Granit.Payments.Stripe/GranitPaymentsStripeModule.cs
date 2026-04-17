using Granit.Http.Resilience;
using Granit.Http.Resilience.Extensions;
using Granit.Modularity;
using Granit.Payments.HealthChecks;
using Granit.Payments.Stripe.Internal;
using Granit.Payments.Stripe.Options;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Granit.Payments.Stripe;

/// <summary>Stripe payment provider for Granit.Payments.</summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsStripeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddGranitHttpClient("Stripe");
        context.Services.AddScoped<StripeClientFactory>();
        context.Services.AddScoped<IStripeClient>(sp =>
            sp.GetRequiredService<StripeClientFactory>().Create());

        context.Services.AddScoped<IPaymentProvider, StripePaymentProvider>();
        context.Services.AddScoped<ICheckoutSessionFactory, StripeCheckoutSessionFactory>();
        context.Services.AddScoped<IPaymentMethodManager, StripePaymentMethodManager>();
        context.Services.AddScoped<IPaymentWebhookVerifier, StripeWebhookVerifier>();

        context.Services.AddHealthChecks().AddGranitPaymentProviderHealthCheck("stripe");
    }
}
