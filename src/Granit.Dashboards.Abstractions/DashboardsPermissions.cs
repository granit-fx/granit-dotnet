namespace Granit.Dashboards;

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

    /// <summary>Permissions on the persisted Dashboard aggregate.</summary>
    public static class Instances
    {
        /// <summary>
        /// Grants read access to the tenant's persisted Dashboards — list, read-by-id,
        /// drift-detection. Required by every read-side endpoint on the aggregate.
        /// </summary>
        public const string Read = "Dashboards.Instances.Read";

        /// <summary>
        /// Grants write access to the persisted Dashboard aggregate — covers import (deep-copy
        /// from a registered DashboardDefinition), state transitions (publish / archive /
        /// restore), and the upcoming widget-edit endpoints.
        /// </summary>
        public const string Manage = "Dashboards.Instances.Manage";
    }
}
