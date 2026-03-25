namespace Granit.Authentication.ApiKeys.Endpoints.Permissions;

/// <summary>
/// Permission constants for API key management endpoints.
/// </summary>
public static class ApiKeyPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "AuthenticationApiKeys";

    /// <summary>Permissions for API key administration.</summary>
    public static class Keys
    {
        /// <summary>Permission to list and view API keys.</summary>
        public const string Read = "AuthenticationApiKeys.Keys.Read";

        /// <summary>Permission to create new API keys.</summary>
        public const string Create = "AuthenticationApiKeys.Keys.Create";

        /// <summary>Permission to revoke API keys.</summary>
        public const string Revoke = "AuthenticationApiKeys.Keys.Revoke";

        /// <summary>Permission to rotate API keys.</summary>
        public const string Rotate = "AuthenticationApiKeys.Keys.Rotate";

        /// <summary>Permission to update permissions and CIDR scopes.</summary>
        public const string UpdateScopes = "AuthenticationApiKeys.Keys.UpdateScopes";
    }
}
