namespace Granit.Dashboards.EntityFrameworkCore;

/// <summary>
/// Database schema and table-name conventions for the <c>Granit.Dashboards</c>
/// persistence layer.
/// </summary>
public static class GranitDashboardsDbProperties
{
    /// <summary>Default schema (PostgreSQL). Hosts can override at <c>OnModelCreating</c> level.</summary>
    public const string DbSchema = "granit_dashboards";

    /// <summary>
    /// Table-name prefix shared by all rows in this module — keeps the SQL surface
    /// readable when the application uses a single shared schema.
    /// </summary>
    public const string DbTablePrefix = "dashboard_";
}
