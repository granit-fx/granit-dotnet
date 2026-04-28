namespace Granit.Dashboards;

/// <summary>
/// Named view of a dashboard — a separate widget arrangement within the same
/// <see cref="DashboardDefinition"/>. Views share the dashboard-level
/// <c>TimeWindow</c>, entity aliases (story P2.3) and breadcrumb context, but each
/// view ships its own widget pool and optional layout override. P2.1 of the
/// dashboards-architecture-proposals roadmap — renamed from "DashboardState" to
/// avoid the clash with <c>Dashboard.Status</c> (Draft / Published / Archived).
/// </summary>
/// <remarks>
/// <para>
/// Single-view dashboards leave <see cref="DashboardDefinition.Views"/> as
/// <c>null</c> and ship their widgets via the inherited <c>Widgets</c> property —
/// the existing default. Multi-view dashboards override <c>Views</c> with one or
/// more <see cref="DashboardView"/> entries and the runtime renders the entry
/// named by <see cref="DashboardDefinition.DefaultView"/> (or the first view if
/// <c>DefaultView</c> is <c>null</c>).
/// </para>
/// <para>
/// View transitions are triggered by widget actions
/// (<c>WidgetActionKind.OpenDashboardView</c>) and reflected in the URL as
/// <c>/dashboards/{Name}/view/{viewName}</c>. Entity aliases that resolve from
/// the active view's parameters (story P2.3 / <c>StateEntityResolver</c>) re-fire
/// at every view transition.
/// </para>
/// </remarks>
/// <param name="Name">
/// View identifier, unique within the dashboard. Lowercase, dot-separated for sub-views
/// (e.g. <c>"list"</c>, <c>"detail"</c>, <c>"history.heatmap"</c>). Used in URLs.
/// </param>
/// <param name="Widgets">Widgets shipped by this view, in declared order.</param>
/// <param name="Layout">
/// Layout override scoped to this view. <c>null</c> = inherit the dashboard's
/// <see cref="DashboardDefinition.Layout"/>. Per-breakpoint overrides on
/// <see cref="DashboardLayout"/> (P1.4) compose normally.
/// </param>
/// <param name="DisplayNameLocalizationKey">
/// Localization key for the view's display label, used in breadcrumbs and the
/// view-switcher control. Defaults to the convention
/// <c>Dashboard:{DashboardName}.View.{ViewName}</c> when <c>null</c>.
/// </param>
public sealed record DashboardView(
    string Name,
    IReadOnlyList<WidgetDefinition> Widgets,
    DashboardLayout? Layout = null,
    string? DisplayNameLocalizationKey = null);
