using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation.UnitedKingdom;

/// <summary>
/// Granit module for United Kingdom identifier, tax, and address validation.
/// </summary>
[DependsOn(
    typeof(GranitLocalizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitValidationUnitedKingdomModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<ValidationUnitedKingdomLocalizationResource>("en")
                .AddJson(
                    typeof(ValidationUnitedKingdomLocalizationResource).Assembly,
                    "Granit.Validation.UnitedKingdom.Localization.ValidationUnitedKingdom");
        });
    }
}
