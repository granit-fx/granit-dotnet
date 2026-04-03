using Granit.Modularity;
using Granit.Payments.Extensions;
using Granit.Timing;

namespace Granit.Payments;

/// <summary>
/// Granit module for provider-agnostic payment processing.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitPaymentsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitPayments();
}
