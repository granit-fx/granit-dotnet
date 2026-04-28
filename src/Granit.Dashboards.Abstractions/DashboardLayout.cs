namespace Granit.Dashboards;

/// <summary>
/// Coarse layout configuration applied to a dashboard's widget grid. Per ADR-038, the
/// layout is intentionally minimal at the declarative level — the persisted
/// <c>Dashboard</c> aggregate (story B2) and the frontend composer (story B5) own the
/// fine-grained layout.
/// </summary>
/// <param name="Columns">Number of grid columns. Default 12 (standard responsive grid).</param>
/// <param name="RowHeight">Row height in CSS pixels. Default 80.</param>
public sealed record DashboardLayout(int Columns, int RowHeight)
{
    /// <summary>Conventional 12-column responsive grid with 80px row height.</summary>
    public static DashboardLayout Default => new(12, 80);
}
