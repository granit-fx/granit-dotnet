using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation.Finance;

/// <summary>
/// Granit module for banking and payment identifier validation: IBAN, BIC/SWIFT, SEPA Creditor
/// Identifier, and the domestic routing/clearing codes (US ABA, AU/NZ BSB, Canadian routing, IFSC).
/// </summary>
[DependsOn(
    typeof(GranitLocalizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitValidationFinanceModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<ValidationFinanceLocalizationResource>("en")
                .AddJson(
                    typeof(ValidationFinanceLocalizationResource).Assembly,
                    "Granit.Validation.Finance.Localization.ValidationFinance");
        });
    }
}
