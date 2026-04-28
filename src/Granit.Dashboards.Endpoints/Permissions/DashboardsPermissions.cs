namespace Granit.Dashboards.Endpoints.Permissions;

/// <summary>
/// Permission constants for Granit.Dashboards endpoints.
/// </summary>
public static class DashboardsPermissions
{
    /// <summary>Permission group name (matches the localization key prefix).</summary>
    public const string GroupName = "Dashboards";

    /// <summary>Permissions on the dashboard catalogue endpoint.</summary>
    public static class Catalog
    {
        /// <summary>Grants read access to the dashboard catalogue (list every registered DashboardDefinition).</summary>
        public const string Read = "Dashboards.Catalog.Read";
    }
}
