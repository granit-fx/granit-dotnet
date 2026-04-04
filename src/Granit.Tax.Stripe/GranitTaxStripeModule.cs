using Granit.Http.Resilience;
using Granit.Http.Resilience.Extensions;
using Granit.Invoicing;
using Granit.Modularity;
using Granit.Tax.Stripe.Internal;
using Granit.Tax.Stripe.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stripe;

namespace Granit.Tax.Stripe;

/// <summary>Stripe Tax API integration for Granit.Tax.</summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitInvoicingModule),
    typeof(GranitTaxModule))]
public sealed class GranitTaxStripeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<StripeTaxOptions>()
            .BindConfiguration(StripeTaxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddGranitHttpClient("StripeTax");
        context.Services.AddScoped<IStripeClient>(sp =>
        {
            IOptions<StripeTaxOptions> opts = sp.GetRequiredService<IOptions<StripeTaxOptions>>();
            HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("StripeTax");
            return new StripeClient(
                apiKey: opts.Value.SecretKey,
                httpClient: new SystemNetHttpClient(httpClient));
        });

        context.Services.TryAddScoped<ITaxCalculator, StripeTaxCalculator>();
        context.Services.TryAddScoped<ITaxIdValidator, StripeTaxIdValidator>();
    }
}
