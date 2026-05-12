using Granit.Modularity;

namespace Granit.Analytics;

/// <summary>
/// Granit module for the analytics contracts shared between modules. Hosts the
/// declarative primitives (<c>MetricDefinition</c>, <c>JoinedMetricDefinition</c>,
/// <c>IMetricDefinitionDescriptor</c>, the analytics-flavoured <c>*WidgetDefinition</c>
/// records, and the <c>AddMetricDefinition&lt;&gt;</c> DI helper) so any module can
/// declare metrics or analytics widgets without pulling the full runtime.
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so consumer modules can
/// declare <c>[DependsOn(typeof(GranitAnalyticsAbstractionsModule))]</c> without
/// pulling the full Granit.Analytics runtime.
/// </remarks>
public sealed class GranitAnalyticsAbstractionsModule : GranitModule;
