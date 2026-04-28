using Granit.Modularity;

namespace Granit.Analytics;

/// <summary>
/// Granit module for the analytics contracts shared between modules. Hosts only the
/// declarative primitives that need to be reused across the framework — currently
/// <see cref="PeriodSpec"/>; future analytics abstractions land here as they get
/// extracted from the runtime package.
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so consumer modules can
/// declare <c>[DependsOn(typeof(GranitAnalyticsAbstractionsModule))]</c> without
/// pulling the full Granit.Analytics runtime.
/// </remarks>
public sealed class GranitAnalyticsAbstractionsModule : GranitModule;
