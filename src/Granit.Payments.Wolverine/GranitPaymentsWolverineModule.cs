using Granit.Modularity;
using Granit.Wolverine;

namespace Granit.Payments.Wolverine;

/// <summary>Wolverine integration for Granit.Payments.</summary>
[DependsOn(
    typeof(GranitPaymentsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitPaymentsWolverineModule : GranitModule;
