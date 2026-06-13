using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Federated.Keycloak.Options;

/// <summary>
/// Configuration options for the Keycloak Admin API used by
/// <see cref="Internal.KeycloakIdentityProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Minimum required role: <c>realm-management:view-users</c>.
/// </para>
/// <para>
/// Additional roles depending on features used:
/// <list type="bullet">
///   <item><description><c>realm-management:manage-users</c> — required for write operations (enable/disable, roles, sessions, password, creation, groups).</description></item>
///   <item><description><c>realm-management:impersonation</c> + feature <c>admin-fine-grained-authz</c> — required when <see cref="UseTokenExchangeForDeviceActivity"/> is <c>true</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// Credentials must be loaded from Vault — never stored in plain text.
/// </para>
/// </remarks>
public sealed class KeycloakAdminOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Federated:Keycloak";

    /// <summary>
    /// Keycloak server base URL (e.g. <c>https://keycloak.example.com</c>).
    /// Must not include the realm path.
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak realm name (e.g. <c>my-company</c>).
    /// </summary>
    [Required]
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Service account client ID with <c>realm-management:view-users</c> role.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Service account client secret. Must be loaded from Vault at runtime.
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, device listing uses the Keycloak Account API
    /// (<c>GET /realms/{realm}/account/sessions/devices</c>) via OAuth 2.0 token exchange,
    /// which provides device-level details (OS, browser, device type).
    /// </summary>
    /// <remarks>
    /// Requires:
    /// <list type="bullet">
    ///   <item><description>Feature <c>admin-fine-grained-authz</c> enabled on the Keycloak realm.</description></item>
    ///   <item><description>Role <c>realm-management:impersonation</c> assigned to the service account.</description></item>
    /// </list>
    /// When <c>false</c> (default), falls back to the Admin API sessions endpoint — device OS,
    /// browser and device type fields will be <c>null</c>.
    /// </remarks>
    public bool UseTokenExchangeForDeviceActivity { get; set; }

    /// <summary>HTTP request timeout in seconds for Keycloak Admin API calls. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Public Keycloak client ID with <c>Direct Access Grants</c> enabled,
    /// used to verify user credentials via the Resource Owner Password Grant.
    /// Required for <see cref="IIdentityCredentialVerifier.VerifyUserCredentialsAsync"/>.
    /// </summary>
    /// <remarks>
    /// This is typically a public client (e.g. <c>my-frontend</c>) — not the confidential
    /// service account client. It must have the <c>Direct Access Grants Enabled</c> flag turned on
    /// in Keycloak.
    /// </remarks>
    public string? DirectAccessClientId { get; set; }

    /// <summary>
    /// Builds the token endpoint URL for the <c>client_credentials</c> flow.
    /// </summary>
    internal string GetTokenEndpoint() =>
        $"{BaseUrl.TrimEnd('/')}/realms/{Realm}/protocol/openid-connect/token";

    /// <summary>
    /// Builds the Admin API URL for listing users assigned to a realm role.
    /// </summary>
    internal string GetRoleUsersEndpoint(string roleName) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/roles/{Uri.EscapeDataString(roleName)}/users";

    /// <summary>
    /// Builds the Admin API URL for listing users with optional search and pagination.
    /// </summary>
    internal string GetUsersEndpoint(string? search = null, int? first = null, int? max = null)
    {
        string baseUrl = $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users";
        List<string> queryParams = [];

        if (!string.IsNullOrEmpty(search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (first.HasValue)
        {
            queryParams.Add($"first={first.Value}");
        }

        if (max.HasValue)
        {
            queryParams.Add($"max={max.Value}");
        }

        return queryParams.Count > 0
            ? $"{baseUrl}?{string.Join('&', queryParams)}"
            : baseUrl;
    }

    /// <summary>
    /// Builds the Admin API URL for getting a single user by ID.
    /// </summary>
    internal string GetUserEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}";

    /// <summary>
    /// Builds the Admin API URL for listing realm roles.
    /// </summary>
    internal string GetRolesEndpoint() =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/roles";

    /// <summary>
    /// Builds the Admin API URL for listing active sessions of a user.
    /// </summary>
    internal string GetUserSessionsEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/sessions";

    /// <summary>
    /// Builds the Admin API URL for listing credentials of a user.
    /// </summary>
    internal string GetUserCredentialsEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/credentials";

    /// <summary>
    /// Builds the Account API URL for listing device activity of the authenticated user.
    /// The user is identified by the bearer token — use with a token obtained via token exchange.
    /// </summary>
    internal string GetAccountSessionsDevicesEndpoint() =>
        $"{BaseUrl.TrimEnd('/')}/realms/{Realm}/account/sessions/devices";

    // ──── Feature 1: User role management ────

    /// <summary>
    /// Builds the Admin API URL for listing/managing realm-level role mappings for a user.
    /// Used for GET (list), POST (assign), DELETE (remove) operations.
    /// </summary>
    internal string GetUserRealmRoleMappingsEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm";

    /// <summary>
    /// Builds the Admin API URL for getting a single role by name.
    /// </summary>
    internal string GetRoleByNameEndpoint(string roleName) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/roles/{Uri.EscapeDataString(roleName)}";

    // ──── Client role support (Phase 2) ────

    /// <summary>
    /// Builds the Admin API URL for listing all OIDC clients in the realm. Optionally filters
    /// by <paramref name="clientId"/> — used to resolve Keycloak's internal client UUID from
    /// the OIDC client_id string.
    /// </summary>
    internal string GetClientsEndpoint(string? clientId = null) =>
        clientId is null
            ? $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/clients"
            : $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/clients?clientId={Uri.EscapeDataString(clientId)}";

    /// <summary>
    /// Builds the Admin API URL for a single client by its internal UUID — the full representation,
    /// including the client's <c>attributes</c> bag (used to read the declared device kind).
    /// </summary>
    internal string GetClientByUuidEndpoint(string clientUuid) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/clients/{Uri.EscapeDataString(clientUuid)}";

    /// <summary>
    /// Builds the Admin API URL for listing client-scope roles of the given Keycloak client
    /// (identified by its internal UUID, not the OIDC client_id).
    /// </summary>
    internal string GetClientRolesEndpoint(string clientUuid) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/clients/{Uri.EscapeDataString(clientUuid)}/roles";

    /// <summary>
    /// Builds the Admin API URL for a specific client role on the given client
    /// (identified by its internal UUID). Used by the Phase 3 write path to resolve the
    /// role's provider-assigned id before POSTing / DELETEing role-mapping payloads.
    /// </summary>
    internal string GetClientRoleByNameEndpoint(string clientUuid, string roleName) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/clients/{Uri.EscapeDataString(clientUuid)}/roles/{Uri.EscapeDataString(roleName)}";

    /// <summary>
    /// Builds the Admin API URL for listing client-scope role mappings of a user for a given
    /// Keycloak client (identified by its internal UUID).
    /// </summary>
    internal string GetUserClientRoleMappingsEndpoint(string userId, string clientUuid) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/role-mappings/clients/{Uri.EscapeDataString(clientUuid)}";

    // ──── Feature 2: Session termination ────

    /// <summary>
    /// Builds the Admin API URL for deleting a specific session.
    /// </summary>
    internal string GetSessionEndpoint(string sessionId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/sessions/{Uri.EscapeDataString(sessionId)}";

    /// <summary>
    /// Builds the Admin API URL for logging out all sessions of a user.
    /// </summary>
    internal string GetUserLogoutEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/logout";

    // ──── Feature 3: Password reset ────

    /// <summary>
    /// Builds the Admin API URL for sending required action emails to a user (e.g. UPDATE_PASSWORD).
    /// </summary>
    internal string GetExecuteActionsEmailEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/execute-actions-email";

    /// <summary>
    /// Builds the Admin API URL for resetting a user's password.
    /// </summary>
    internal string GetResetPasswordEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/reset-password";

    // ──── Feature 5: Group management ────

    /// <summary>
    /// Builds the Admin API URL for listing all groups.
    /// </summary>
    internal string GetGroupsEndpoint() =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/groups";

    /// <summary>
    /// Builds the Admin API URL for listing groups of a user.
    /// </summary>
    internal string GetUserGroupsEndpoint(string userId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/groups";

    /// <summary>
    /// Builds the Admin API URL for managing a user's membership in a specific group.
    /// Used for PUT (add) and DELETE (remove) operations.
    /// </summary>
    internal string GetUserGroupMembershipEndpoint(string userId, string groupId) =>
        $"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/groups/{Uri.EscapeDataString(groupId)}";
}
