namespace Granit.Dashboards;

/// <summary>
/// Declarative click-handler descriptor attached to a <see cref="WidgetDefinition"/>.
/// No code injection, no expression evaluation — only a typed
/// (<see cref="WidgetActionTrigger"/>, <see cref="WidgetActionKind"/>, target,
/// optional params) tuple that the frontend dispatches to a known handler.
/// See P1.5 of the dashboards-architecture-proposals roadmap.
/// </summary>
/// <param name="Trigger">What user gesture fires the action.</param>
/// <param name="Kind">What kind of dispatch the frontend should perform.</param>
/// <param name="Target">
/// Target identifier. Interpretation depends on <see cref="Kind"/>:
/// <list type="bullet">
///   <item><see cref="WidgetActionKind.Navigate"/> — frontend route (e.g. <c>"/invoicing?status=unpaid"</c>).</item>
///   <item><see cref="WidgetActionKind.OpenDashboardView"/> — a view name on the same dashboard (story P2.1).</item>
///   <item><see cref="WidgetActionKind.OpenDashboard"/> — a <c>Dashboard.Name</c>.</item>
///   <item><see cref="WidgetActionKind.ExportData"/> — an <c>ExportDefinition.Name</c>.</item>
///   <item><see cref="WidgetActionKind.OpenDetail"/> — a side-drawer name (typically the row's full detail).</item>
/// </list>
/// </param>
/// <param name="Params">
/// Static parameters merged with the dynamic data row at dispatch time. Values may
/// reference variables resolved by <c>IVariableSubstituter</c> (P3.3) — for example
/// <c>{"customerId": "${row.customerId}", "status": "unpaid"}</c>. <c>null</c> = no
/// extra parameters beyond the row data itself.
/// </param>
public sealed record WidgetAction(
    WidgetActionTrigger Trigger,
    WidgetActionKind Kind,
    string Target,
    IReadOnlyDictionary<string, string>? Params = null);

/// <summary>What user gesture fires a <see cref="WidgetAction"/>.</summary>
public enum WidgetActionTrigger
{
    /// <summary>Clicking the widget body or KPI value.</summary>
    Click = 0,

    /// <summary>Clicking a row inside a Table / Pivot widget.</summary>
    RowClick = 1,

    /// <summary>Clicking a series segment inside a Chart widget (a bar, a line point, a slice).</summary>
    SeriesClick = 2,

    /// <summary>Clicking a legend item inside a Chart widget.</summary>
    LegendClick = 3,
}

/// <summary>Type of dispatch the frontend performs when a <see cref="WidgetAction"/> fires.</summary>
public enum WidgetActionKind
{
    /// <summary>Frontend route navigation (preserves dashboard context if possible).</summary>
    Navigate = 0,

    /// <summary>
    /// Switch to another <c>view</c> of the current dashboard — see story P2.1
    /// (Views, intra-dashboard navigation; renamed from "DashboardState" to avoid
    /// the clash with the persisted <c>Dashboard.Status</c> field).
    /// </summary>
    OpenDashboardView = 1,

    /// <summary>Navigate to another full <see cref="DashboardDefinition"/> by name.</summary>
    OpenDashboard = 2,

    /// <summary>Trigger an export via <c>Granit.DataExchange</c> (target = <c>ExportDefinition.Name</c>).</summary>
    ExportData = 3,

    /// <summary>Open a side drawer with the row's full detail (typical for table widgets).</summary>
    OpenDetail = 4,
}
