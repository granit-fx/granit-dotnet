namespace Granit.Identity.Local.Endpoints.Permissions;

/// <summary>
/// Permission constants for the Identity.Local module.
/// </summary>
/// <remarks>
/// All permission strings follow the <c>[Group].[Resource].[Action]</c> format.
/// Registered via <c>IdentityLocalPermissionDefinitionProvider</c> (auto-discovered
/// by <c>GranitAuthorizationModule</c>).
/// </remarks>
public static class IdentityLocalPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "IdentityLocal";

    /// <summary>Permissions for user impersonation.</summary>
    public static class Users
    {
        /// <summary>Permission to impersonate a user by issuing a short-lived token.</summary>
        public const string Impersonate = "IdentityLocal.Users.Impersonate";
    }

    /// <summary>Permissions for the local role CRUD endpoints.</summary>
    public static class Roles
    {
        /// <summary>Read / list local roles visible in the caller's context.</summary>
        public const string Read = "IdentityLocal.Roles.Read";

        /// <summary>Create or rename local roles.</summary>
        public const string Manage = "IdentityLocal.Roles.Manage";

        /// <summary>Hard-delete non-system roles.</summary>
        public const string Delete = "IdentityLocal.Roles.Delete";
    }
}
