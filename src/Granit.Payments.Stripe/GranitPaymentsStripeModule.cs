using Granit.Modularity;
using Granit.Payments.Stripe.Internal;
using Granit.Payments.Stripe.Options;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Granit.Payments.Stripe;

/// <summary>Stripe payment provider for Granit.Payments.</summary>
[DependsOn(typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsStripeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddHttpClient("Stripe");
        context.Services.AddScoped<StripeClientFactory>();
        context.Services.AddScoped<IStripeClient>(sp =>
            sp.GetRequiredService<StripeClientFactory>().Create());

        context.Services.AddScoped<IPaymentProvider, StripePaymentProvider>();
        context.Services.AddScoped<ICheckoutSessionFactory, StripeCheckoutSessionFactory>();
        context.Services.AddScoped<IPaymentMethodManager, StripePaymentMethodManager>();
        context.Services.AddSingleton<IPaymentWebhookVerifier, StripeWebhookVerifier>();
    }
}
