namespace Granit.Auditing.Domain;

/// <summary>
/// Audit log categories inspired by Google Cloud Audit Logs.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><see cref="DataMutation"/> — entity CRUD (always logged).</item>
///   <item><see cref="ConfigurationChange"/> — settings, feature flags (always logged, non-disableable).</item>
///   <item><see cref="DataAccess"/> — read operations (opt-in, disabled by default).</item>
///   <item><see cref="AccessDenied"/> — authorization failures (opt-in).</item>
///   <item><see cref="PrivilegedAccess"/> — privileged-access operations that succeeded (e.g. host tenant impersonation). Counterpart of <see cref="AccessDenied"/>: ISO 27001 A.12.4.3 wants successful privileged ops in their own bucket.</item>
/// </list>
/// </remarks>
public enum AuditCategory
{
    /// <summary>Entity Create / Update / Delete / SoftDelete operations.</summary>
    DataMutation = 0,

    /// <summary>Settings and feature flag changes (always-on, non-disableable).</summary>
    ConfigurationChange = 1,

    /// <summary>Read operations (opt-in, disabled by default).</summary>
    DataAccess = 2,

    /// <summary>Authorization failures (opt-in).</summary>
    AccessDenied = 3,

    /// <summary>
    /// Privileged-access operations that were granted (e.g. host tenant impersonation, admin masquerading).
    /// Recorded as a counterpart to <see cref="AccessDenied"/> so RSSI dashboards can answer
    /// "what did privileged actors successfully do" without joining against logs/metrics.
    /// </summary>
    PrivilegedAccess = 4,
}
