namespace Granit.Diagnostics.Endpoints.Permissions;

/// <summary>
/// Permission constants for diagnostics monitoring endpoints.
/// </summary>
public static class DiagnosticsPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Diagnostics";

    /// <summary>Monitoring dashboard permissions.</summary>
    public static class Monitoring
    {
        /// <summary>View the aggregated health status of all registered services.</summary>
        public const string Read = "Diagnostics.Monitoring.Read";
    }
}
