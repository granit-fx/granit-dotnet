using System.Diagnostics.CodeAnalysis;

namespace Granit.Identity.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Identity.Endpoints</c> module.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Permission resource names follow [Module].[Resource].[Action] convention")]
public static class IdentityPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Identity";

    /// <summary>Permissions for user management (cache and identity provider).</summary>
    public static class Users
    {
        /// <summary>Grants access to list, search, and view users (cache and provider).</summary>
        public const string Read = "Identity.Users.Read";

        /// <summary>Grants access to create, update, and enable/disable users in the identity provider.</summary>
        public const string Manage = "Identity.Users.Manage";

        /// <summary>Grants access to force sync (single or full) from the identity provider.</summary>
        public const string Sync = "Identity.Users.Sync";

        /// <summary>Grants access to RGPD erasure and pseudonymization.</summary>
        public const string Delete = "Identity.Users.Delete";
    }

    /// <summary>Permissions for managing roles via the identity provider.</summary>
    public static class Roles
    {
        /// <summary>Grants access to list roles, view user roles, and list role members.</summary>
        public const string Read = "Identity.Roles.Read";

        /// <summary>Grants access to assign and remove roles.</summary>
        public const string Manage = "Identity.Roles.Manage";
    }

    /// <summary>Permissions for managing groups via the identity provider.</summary>
    public static class Groups
    {
        /// <summary>Grants access to list groups and view user group memberships.</summary>
        public const string Read = "Identity.Groups.Read";

        /// <summary>Grants access to add and remove users from groups.</summary>
        public const string Manage = "Identity.Groups.Manage";
    }

    /// <summary>Permissions for managing user sessions.</summary>
    public static class Sessions
    {
        /// <summary>Grants access to view user sessions and device activity.</summary>
        public const string Read = "Identity.Sessions.Read";

        /// <summary>Grants access to terminate user sessions.</summary>
        public const string Manage = "Identity.Sessions.Manage";
    }

    /// <summary>Permissions for managing user passwords.</summary>
    public static class Passwords
    {
        /// <summary>Grants access to password operations (reset email, temporary password, change history).</summary>
        public const string Manage = "Identity.Passwords.Manage";
    }
}
