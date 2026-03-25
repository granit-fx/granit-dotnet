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
}
