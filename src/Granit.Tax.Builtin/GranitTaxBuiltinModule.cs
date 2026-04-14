using Granit.Caching;
using Granit.Http.Resilience;
using Granit.Http.Resilience.Extensions;
using Granit.Invoicing;
using Granit.Modularity;
using Granit.Tax.Builtin.Internal;
using Granit.Tax.Builtin.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Tax.Builtin;

/// <summary>
/// Self-hosted EU VAT tax calculator. Implements <see cref="ITaxCalculator"/>
/// with EU VAT rules (reverse charge, OSS, export) and VIES online validation.
/// </summary>
[DependsOn(
    typeof(GranitCachingModule),
    typeof(GranitHttpResilienceModule),
    typeof(GranitInvoicingModule),
    typeof(GranitTaxModule))]
public sealed class GranitTaxBuiltinModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<EuVatRateOptions>()
            .BindConfiguration(EuVatRateOptions.SectionName);

        context.Services.AddOptions<ViesOptions>()
            .BindConfiguration(ViesOptions.SectionName);

        context.Services.AddGranitHttpClient("Vies", (sp, client) =>
        {
            ViesOptions viesOptions = sp.GetRequiredService<IOptions<ViesOptions>>().Value;
            client.BaseAddress = viesOptions.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        context.Services.TryAddScoped<ITaxCalculator, EuVatTaxCalculator>();
        context.Services.TryAddScoped<ITaxIdValidator, ViesValidator>();
        context.Services.TryAddSingleton<ITaxRateProvider, ConfigTaxRateProvider>();
    }
}
