namespace Granit.Dashboards;

/// <summary>
/// Layout configuration for a dashboard's widget grid. Carries a base configuration
/// applied at every viewport plus optional per-breakpoint overrides — same model
/// ThingsBoard ships, scaled down to one grid (the dual-pane container is deferred
/// to v2 — see ADR-038).
/// </summary>
/// <param name="Columns">Number of grid columns at the base viewport. Default 12.</param>
/// <param name="RowHeight">Row height in CSS pixels at the base viewport. Default 80.</param>
/// <param name="WidgetSizes">
/// Per-widget size overrides keyed by <c>WidgetDefinition.Slug</c>. <c>null</c> = use
/// the widget's default <c>Size</c> from its definition.
/// </param>
/// <param name="WidgetOrder">
/// Optional Slug ordering — when present, overrides the widget's <c>Position</c> for
/// this layout. Widgets not listed are appended in declared <c>Position</c> order.
/// </param>
/// <param name="Breakpoints">
/// Per-breakpoint overrides. Each entry is a partial layout merged onto the base when
/// the viewport hits the corresponding <see cref="DashboardBreakpoint"/>. Missing
/// breakpoints fall through to the base.
/// </param>
public sealed record DashboardLayout(
    int Columns,
    int RowHeight,
    IReadOnlyDictionary<string, WidgetSize>? WidgetSizes = null,
    IReadOnlyList<string>? WidgetOrder = null,
    IReadOnlyDictionary<DashboardBreakpoint, DashboardLayoutOverride>? Breakpoints = null)
{
    /// <summary>Conventional 12-column responsive grid with 80px row height and no overrides.</summary>
    public static DashboardLayout Default => new(12, 80);
}

/// <summary>
/// Tailwind / Bootstrap-style viewport breakpoints. The <see cref="DashboardLayout.Breakpoints"/>
/// dictionary lets a dashboard ship a different layout per breakpoint without forking
/// the widget pool.
/// </summary>
public enum DashboardBreakpoint
{
    /// <summary>Extra-small (mobile portrait).</summary>
    Xs = 0,

    /// <summary>Small (mobile landscape, small tablets).</summary>
    Sm = 1,

    /// <summary>Medium (tablets, narrow desktops).</summary>
    Md = 2,

    /// <summary>Large (desktops).</summary>
    Lg = 3,

    /// <summary>Extra-large (wide desktops, large monitors).</summary>
    Xl = 4,
}

/// <summary>
/// Partial layout — merged onto the base <see cref="DashboardLayout"/> when a viewport
/// hits the matching <see cref="DashboardBreakpoint"/>. Every field is nullable so
/// overrides stay scoped to what actually changes per breakpoint.
/// </summary>
/// <param name="Columns">Override the base column count at this breakpoint.</param>
/// <param name="RowHeight">Override the base row height at this breakpoint.</param>
/// <param name="WidgetSizes">Per-widget size overrides keyed by <c>WidgetDefinition.Slug</c>.</param>
/// <param name="WidgetOrder">Override the widget order at this breakpoint.</param>
/// <param name="HiddenWidgets">
/// Slugs to hide at this breakpoint. The widgets remain in the pool — only the layout
/// drops them, which keeps state intact when the viewport flips back.
/// </param>
public sealed record DashboardLayoutOverride(
    int? Columns = null,
    int? RowHeight = null,
    IReadOnlyDictionary<string, WidgetSize>? WidgetSizes = null,
    IReadOnlyList<string>? WidgetOrder = null,
    IReadOnlySet<string>? HiddenWidgets = null);
