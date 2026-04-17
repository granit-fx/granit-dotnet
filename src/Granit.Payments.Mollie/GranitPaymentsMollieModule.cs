using Granit.Modularity;
using Granit.Payments.HealthChecks;
using Granit.Payments.Mollie.Internal;
using Granit.Payments.Mollie.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;

namespace Granit.Payments.Mollie;

/// <summary>Mollie payment provider for Granit.Payments (EU-sovereign PSP).</summary>
[DependsOn(typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsMollieModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<MollieOptions>()
            .BindConfiguration(MollieOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register Mollie API clients with API key from options
        context.Services.AddScoped<IPaymentClient>(sp =>
        {
            IOptions<MollieOptions> options = sp.GetRequiredService<IOptions<MollieOptions>>();
            return new PaymentClient(options.Value.ApiKey);
        });

        context.Services.AddScoped<IRefundClient>(sp =>
        {
            IOptions<MollieOptions> options = sp.GetRequiredService<IOptions<MollieOptions>>();
            return new RefundClient(options.Value.ApiKey);
        });

        context.Services.AddScoped<IPaymentProvider, MolliePaymentProvider>();
        context.Services.AddScoped<ICheckoutSessionFactory, MollieCheckoutSessionFactory>();
        context.Services.AddSingleton<IPaymentMethodManager, MolliePaymentMethodManager>();
        context.Services.AddScoped<IPaymentWebhookVerifier, MollieWebhookVerifier>();

        context.Services.AddHealthChecks().AddGranitPaymentProviderHealthCheck("mollie");
    }
}
