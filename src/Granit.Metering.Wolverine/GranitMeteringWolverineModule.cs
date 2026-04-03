using Granit.Modularity;
using Granit.Wolverine;

namespace Granit.Metering.Wolverine;

/// <summary>
/// Wolverine integration for Granit.Metering.
/// </summary>
[DependsOn(
    typeof(GranitMeteringModule),
    typeof(GranitWolverineModule))]
public sealed class GranitMeteringWolverineModule : GranitModule;
