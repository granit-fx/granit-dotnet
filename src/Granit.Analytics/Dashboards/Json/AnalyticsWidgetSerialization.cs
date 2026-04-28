using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards.Json;

namespace Granit.Analytics.Dashboards.Json;

/// <summary>
/// Convenience helpers wiring the four analytics-flavoured widget kinds
/// (<see cref="KpiWidgetDefinition"/>, <see cref="ChartWidgetDefinition"/>,
/// <see cref="TableWidgetDefinition"/>, <see cref="PivotWidgetDefinition"/>) into
/// the JSON polymorphism chain shared by the dashboards stack.
/// </summary>
/// <remarks>
/// Hosts that expose dashboard endpoints call
/// <see cref="AddAnalyticsWidgets(JsonSerializerOptions)"/> once on their shared
/// <see cref="JsonSerializerOptions"/> at startup. The discriminators (<c>"kpi"</c>,
/// <c>"chart"</c>, <c>"table"</c>, <c>"pivot"</c>) become the contract surfaced to
/// frontends and stay stable across module upgrades.
/// </remarks>
public static class AnalyticsWidgetSerialization
{
    /// <summary>Registers the four analytics widget kinds on the supplied options.</summary>
    /// <param name="options">Target serializer options.</param>
    /// <returns>The same <paramref name="options"/> for chaining.</returns>
    public static JsonSerializerOptions AddAnalyticsWidgets(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddDerivedType<KpiWidgetDefinition>("kpi");
        options.AddDerivedType<ChartWidgetDefinition>("chart");
        options.AddDerivedType<TableWidgetDefinition>("table");
        options.AddDerivedType<PivotWidgetDefinition>("pivot");

        return options;
    }
}
