namespace Granit.AuditLog.Endpoints.Permissions;

/// <summary>
/// Permission constants for audit log endpoints.
/// </summary>
public static class AuditLogPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "AuditLog";

    /// <summary>Permissions for the audit log entries resource.</summary>
    public static class Entries
    {
        /// <summary>Grants read access to audit log entries (ISO 27001 audit trail).</summary>
        public const string Read = "AuditLog.Entries.Read";
    }
}
