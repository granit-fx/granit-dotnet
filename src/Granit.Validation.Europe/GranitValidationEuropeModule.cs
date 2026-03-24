using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation.Europe;

/// <summary>
/// Granit module for European regulatory identifier validation (France, Belgium).
/// </summary>
[DependsOn(
    typeof(GranitLocalizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitValidationEuropeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<ValidationEuropeLocalizationResource>("fr")
                .AddJson(
                    typeof(ValidationEuropeLocalizationResource).Assembly,
                    "Granit.Validation.Europe.Localization.ValidationEurope");
        });
    }
}
