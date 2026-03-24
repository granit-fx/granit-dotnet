using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation.NorthAmerica;

/// <summary>
/// Granit module for North American identifier and address validation (United States, Canada).
/// </summary>
[DependsOn(
    typeof(GranitLocalizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitValidationNorthAmericaModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<ValidationNorthAmericaLocalizationResource>("en")
                .AddJson(
                    typeof(ValidationNorthAmericaLocalizationResource).Assembly,
                    "Granit.Validation.NorthAmerica.Localization.ValidationNorthAmerica");
        });
    }
}
