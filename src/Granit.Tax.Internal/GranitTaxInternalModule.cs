using Granit.Caching;
using Granit.Invoicing;
using Granit.Modularity;
using Granit.Tax.Internal.Internal;
using Granit.Tax.Internal.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Tax.Internal;

/// <summary>
/// Self-hosted EU VAT tax calculator. Implements <see cref="ITaxCalculator"/>
/// with EU VAT rules (reverse charge, OSS, export) and VIES online validation.
/// </summary>
[DependsOn(
    typeof(GranitCachingModule),
    typeof(GranitInvoicingModule),
    typeof(GranitTaxModule))]
public sealed class GranitTaxInternalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<EuVatRateOptions>()
            .BindConfiguration(EuVatRateOptions.SectionName);

        context.Services.AddHttpClient("Vies", client =>
        {
            client.BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/");
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddStandardResilienceHandler();

        context.Services.TryAddScoped<ITaxCalculator, EuVatTaxCalculator>();
        context.Services.TryAddScoped<ITaxIdValidator, ViesValidator>();
        context.Services.TryAddSingleton<ITaxRateProvider, ConfigTaxRateProvider>();
    }
}
