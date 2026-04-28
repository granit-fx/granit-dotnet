namespace Granit.Dashboards;

/// <summary>
/// Width / height of a widget on the dashboard grid, expressed in grid cells.
/// The frontend grid is conventionally 12 columns wide; widget heights are typically
/// 1–6 rows. The runtime does not enforce a maximum — the rendering layer clamps
/// values to its viewport.
/// </summary>
/// <param name="Width">Grid columns occupied by the widget. Must be &gt; 0.</param>
/// <param name="Height">Grid rows occupied by the widget. Must be &gt; 0.</param>
public sealed record WidgetSize(int Width, int Height)
{
    /// <summary>A small KPI card — 3×1 (a quarter row).</summary>
    public static WidgetSize SmallKpi => new(3, 1);

    /// <summary>A standard chart — half a row, two rows tall (6×2).</summary>
    public static WidgetSize StandardChart => new(6, 2);

    /// <summary>A full-width content widget (12×1) — typical for markdown banners.</summary>
    public static WidgetSize FullWidthRow => new(12, 1);

    /// <summary>A square media tile (4×4) — typical default for image / video widgets.</summary>
    public static WidgetSize MediaTile => new(4, 4);
}
