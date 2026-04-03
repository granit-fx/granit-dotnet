using Granit.Modularity;
using Granit.Tax.Extensions;
using Granit.Timing;

namespace Granit.Tax;

/// <summary>
/// Granit module for tax calculation, validation, and compliance.
/// </summary>
/// <remarks>
/// Provides core abstractions (<see cref="ITaxIdValidator"/>, <see cref="ITaxRateProvider"/>)
/// and domain types. Add a provider package for concrete implementation:
/// <list type="bullet">
/// <item><c>Granit.Tax.Internal</c> — self-hosted EU VAT rules + VIES validation</item>
/// <item><c>Granit.Tax.Stripe</c> — Stripe Tax API integration</item>
/// </list>
/// </remarks>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitTaxModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitTax();
}
