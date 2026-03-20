using System.Diagnostics.CodeAnalysis;

namespace Granit.Identity.Endpoints.Permissions;

/// <summary>
/// Permission constants for identity provider administration endpoints.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Permission resource names follow [Module].[Resource].[Action] convention")]
public static class IdentityProviderPermissions
{
    /// <summary>Permission group name (shared with <see cref="IdentityUserCachePermissions"/>).</summary>
    public const string GroupName = "Identity";

    /// <summary>Permissions for managing users via the identity provider.</summary>
    public static class Users
    {
        /// <summary>Grants access to list and view users directly from the identity provider.</summary>
        public const string Read = "Identity.Users.Read";

        /// <summary>Grants access to create, update, and enable/disable users in the identity provider.</summary>
        public const string Manage = "Identity.Users.Manage";
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
