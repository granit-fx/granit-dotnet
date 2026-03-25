namespace Granit.Auditing.Endpoints.Permissions;

/// <summary>
/// Permission constants for audit log endpoints.
/// </summary>
public static class AuditingPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Auditing";

    /// <summary>Permissions for the audit entries resource.</summary>
    public static class AuditEntries
    {
        /// <summary>Grants read access to audit entries (ISO 27001 audit trail).</summary>
        public const string Read = "Auditing.AuditEntries.Read";
    }
}
