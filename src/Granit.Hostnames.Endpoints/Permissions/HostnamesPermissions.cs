namespace Granit.Hostnames.Endpoints.Permissions;

/// <summary>
/// Permission constants for custom hostname management endpoints.
/// Format: <c>[Group].[Resource].[Action]</c> (PascalCase, plural resource).
/// </summary>
public static class HostnamesPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Hostnames";

    /// <summary>Permissions for the managed hostnames resource.</summary>
    public static class Hostnames
    {
        /// <summary>Grants read access to hostname list and detail.</summary>
        public const string Read = "Hostnames.Hostnames.Read";

        /// <summary>Grants write access: create, delete, set/clear primary, availability check.</summary>
        public const string Manage = "Hostnames.Hostnames.Manage";
    }

    /// <summary>Permissions for the certificate-status webhook endpoint.</summary>
    public static class Certificates
    {
        /// <summary>Grants the edge provider the right to push certificate status updates.</summary>
        public const string Report = "Hostnames.Certificates.Report";
    }
}
