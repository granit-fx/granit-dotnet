using Granit.Modularity;
using Granit.Timing;

namespace Granit.Payments;

/// <summary>
/// Granit module for provider-agnostic payment processing.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitPaymentsModule : GranitModule;
