namespace Granit.Analytics.Endpoints.Permissions;

/// <summary>
/// Permission constants for Granit.Analytics endpoints.
/// </summary>
public static class AnalyticsPermissions
{
    /// <summary>Permission group name (matches the localization key prefix).</summary>
    public const string GroupName = "Analytics";

    /// <summary>Permissions on the metrics endpoint.</summary>
    public static class Metrics
    {
        /// <summary>Grants read access to evaluate metric definitions through the HTTP surface.</summary>
        public const string Read = "Analytics.Metrics.Read";
    }
}
