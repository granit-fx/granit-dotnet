using Granit.Modularity;

namespace Granit.Dashboards;

/// <summary>
/// Granit module for the dashboard contracts. Hosts only the declarative primitives
/// (<see cref="DashboardDefinition"/>, <see cref="WidgetDefinition"/>, layout / size
/// value objects, registry interface) plus the presentation-only widgets
/// (<see cref="Widgets.MarkdownWidgetDefinition"/>, <see cref="Widgets.ImageWidgetDefinition"/>,
/// <see cref="Widgets.TextWidgetDefinition"/>).
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so that consumer modules
/// (Granit.Analytics, future Granit.IoT.Dashboards, ...) can declare
/// <c>[DependsOn(typeof(GranitDashboardsAbstractionsModule))]</c> without pulling in
/// the registry / persistence / endpoints runtime that lives in
/// <c>Granit.Dashboards</c>.
/// </remarks>
public sealed class GranitDashboardsAbstractionsModule : GranitModule;
