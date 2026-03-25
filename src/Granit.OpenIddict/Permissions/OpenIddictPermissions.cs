namespace Granit.OpenIddict.Permissions;

/// <summary>
/// Permission constants for the OpenIddict module.
/// </summary>
/// <remarks>
/// All permission strings follow the <c>[Group].[Resource].[Action]</c> format.
/// Registered via <c>OpenIddictPermissionDefinitionProvider</c> (auto-discovered
/// by <c>GranitAuthorizationModule</c>).
/// </remarks>
public static class OpenIddictPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "OpenIddict";

    /// <summary>Permissions for user impersonation (token issuance).</summary>
    public static class Users
    {
        /// <summary>Permission to impersonate a user by issuing a short-lived token.</summary>
        public const string Impersonate = "OpenIddict.Users.Impersonate";
    }

    /// <summary>Permissions for OIDC application administration.</summary>
    public static class Applications
    {
        /// <summary>Permission to list and view OIDC applications.</summary>
        public const string Read = "OpenIddict.Applications.Read";

        /// <summary>Permission to create new OIDC applications.</summary>
        public const string Create = "OpenIddict.Applications.Create";

        /// <summary>Permission to manage OIDC application details.</summary>
        public const string Manage = "OpenIddict.Applications.Manage";

        /// <summary>Permission to delete OIDC applications.</summary>
        public const string Delete = "OpenIddict.Applications.Delete";

        /// <summary>Permission to rotate OIDC application secrets.</summary>
        public const string Rotate = "OpenIddict.Applications.Rotate";
    }

    /// <summary>Permissions for OIDC scope administration.</summary>
    public static class Scopes
    {
        /// <summary>Permission to list and view OIDC scopes.</summary>
        public const string Read = "OpenIddict.Scopes.Read";

        /// <summary>Permission to create new OIDC scopes.</summary>
        public const string Create = "OpenIddict.Scopes.Create";

        /// <summary>Permission to manage OIDC scope details.</summary>
        public const string Manage = "OpenIddict.Scopes.Manage";

        /// <summary>Permission to delete OIDC scopes.</summary>
        public const string Delete = "OpenIddict.Scopes.Delete";
    }

    /// <summary>Permissions for OIDC authorization administration.</summary>
    public static class Authorizations
    {
        /// <summary>Permission to list and view OIDC authorizations.</summary>
        public const string Read = "OpenIddict.Authorizations.Read";

        /// <summary>Permission to revoke OIDC authorizations.</summary>
        public const string Revoke = "OpenIddict.Authorizations.Revoke";
    }
}
