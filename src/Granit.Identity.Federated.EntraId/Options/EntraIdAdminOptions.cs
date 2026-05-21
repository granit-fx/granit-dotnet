using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Federated.EntraId.Options;

/// <summary>
/// Configuration options for the Microsoft Graph API used by
/// <see cref="Internal.EntraIdIdentityProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Required Azure AD API permissions (Application type):
/// <list type="bullet">
///   <item><description><c>User.ReadWrite.All</c> — user CRUD operations.</description></item>
///   <item><description><c>Group.ReadWrite.All</c> — group membership management.</description></item>
///   <item><description><c>AppRoleAssignment.ReadWrite.All</c> — App Role assignment.</description></item>
///   <item><description><c>AuditLog.Read.All</c> — sign-in activity and session data.</description></item>
///   <item><description><c>Directory.ReadWrite.All</c> — password management.</description></item>
/// </list>
/// </para>
/// <para>
/// Credentials must be loaded from Vault — never stored in plain text.
/// </para>
/// </remarks>
public sealed class EntraIdAdminOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Federated:EntraId";

    /// <summary>
    /// Azure AD tenant ID (e.g. <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>).
    /// </summary>
    [Required]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Service principal client ID for the <c>client_credentials</c> flow.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Service principal client secret. Must be loaded from Vault at runtime.
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Object ID of the Service Principal for App Role operations.
    /// Found in Azure Portal → Enterprise Applications → your app → Object ID.
    /// </summary>
    [Required]
    public string ServicePrincipalObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Default domain for constructing <c>userPrincipalName</c> when creating users.
    /// Example: <c>contoso.onmicrosoft.com</c>.
    /// Required for <see cref="IIdentityUserWriter.CreateUserAsync"/>.
    /// </summary>
    public string? DefaultDomain { get; set; }

    /// <summary>HTTP request timeout in seconds for Microsoft Graph API calls. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Base URL for the Microsoft Graph API. Default: <c>https://graph.microsoft.com</c>.
    /// </summary>
    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com";

    /// <summary>
    /// Public client ID with <c>Allow public client flows</c> enabled,
    /// used to verify user credentials via the Resource Owner Password Credentials (ROPC) flow.
    /// Required for <see cref="IIdentityCredentialVerifier.VerifyUserCredentialsAsync"/>.
    /// </summary>
    public string? RopcClientId { get; set; }

    // ──── Token endpoint ────

    /// <summary>
    /// Builds the token endpoint URL for the <c>client_credentials</c> flow.
    /// </summary>
    internal string GetTokenEndpoint() =>
        $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";

    // ──── Users ────

    /// <summary>
    /// Builds the Graph API URL for listing users with optional search and pagination.
    /// </summary>
    internal static string GetUsersEndpoint(string? search = null, int? skip = null, int? top = null)
    {
        List<string> queryParams =
        [
            "$select=id,userPrincipalName,mail,givenName,surname,accountEnabled"
        ];

        if (!string.IsNullOrEmpty(search))
        {
            queryParams.Add($"$filter=startswith(displayName,'{Uri.EscapeDataString(search)}') or startswith(mail,'{Uri.EscapeDataString(search)}')");
        }

        if (skip.HasValue)
        {
            queryParams.Add($"$skip={skip.Value}");
        }

        if (top.HasValue)
        {
            queryParams.Add($"$top={top.Value}");
        }

        return $"/v1.0/users?{string.Join('&', queryParams)}";
    }

    /// <summary>
    /// Builds the Graph API URL for getting a single user by ID.
    /// </summary>
    internal static string GetUserEndpoint(string userId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}?$select=id,userPrincipalName,mail,givenName,surname,accountEnabled";

    /// <summary>
    /// Builds the Graph API URL for getting a user's last password change date.
    /// </summary>
    internal static string GetUserPasswordChangeDateEndpoint(string userId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}?$select=lastPasswordChangeDateTime";

    // ──── Audit sign-ins (sessions / device activity) ────

    /// <summary>
    /// Builds the Graph API URL for listing sign-in audit logs of a user.
    /// </summary>
    internal static string GetAuditSignInsEndpoint(string userId, int top = 25) =>
        $"/v1.0/auditLogs/signIns?$filter=userId eq '{Uri.EscapeDataString(userId)}'&$top={top}&$orderby=createdDateTime desc";

    // ──── App Roles ────

    /// <summary>
    /// Builds the Graph API URL for listing App Roles on the Service Principal.
    /// </summary>
    internal string GetServicePrincipalAppRolesEndpoint() =>
        $"/v1.0/servicePrincipals/{Uri.EscapeDataString(ServicePrincipalObjectId)}/appRoles";

    /// <summary>
    /// Builds the Graph API URL for listing App Role assignments on the Service Principal.
    /// </summary>
    internal string GetServicePrincipalAppRoleAssignedToEndpoint() =>
        $"/v1.0/servicePrincipals/{Uri.EscapeDataString(ServicePrincipalObjectId)}/appRoleAssignedTo";

    /// <summary>
    /// Builds the Graph API URL for listing App Role assignments for a user.
    /// </summary>
    internal static string GetUserAppRoleAssignmentsEndpoint(string userId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}/appRoleAssignments";

    // ──── Client-role sync (Phase 2) ────

    /// <summary>
    /// Builds the Graph API URL for listing Service Principals. Optionally filters by
    /// <paramref name="appId"/> to resolve the Service Principal <c>id</c> (object id) from an
    /// OIDC <c>appId</c>. Returns `$select=id,appId,appRoles` so the caller can project
    /// both the object id and the inline <c>appRoles</c> array without a second call.
    /// </summary>
    internal static string GetServicePrincipalsEndpoint(string? appId = null)
    {
        const string select = "$select=id,appId,appRoles";
        return appId is null
            ? $"/v1.0/servicePrincipals?{select}"
            : $"/v1.0/servicePrincipals?$filter=appId eq '{Uri.EscapeDataString(appId)}'&{select}";
    }

    /// <summary>
    /// Builds the Graph API URL for listing App Role assignments of a user scoped to a given
    /// Service Principal (identified by its object id — NOT the OIDC <c>appId</c>).
    /// </summary>
    internal static string GetUserAppRoleAssignmentsForServicePrincipalEndpoint(
        string userId, string servicePrincipalObjectId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}/appRoleAssignments" +
        $"?$filter=resourceId eq {servicePrincipalObjectId}";

    // ──── Client-role writes (Phase 3, ADR-031) ────

    /// <summary>
    /// Builds the Graph API URL for listing / projecting Applications filtered by
    /// <paramref name="appId"/>. App Roles are authored on the <c>Application</c> object, NOT
    /// on the Service Principal — Phase 3 <c>CreateClientRoleAsync</c> fetches the application
    /// to learn its <c>id</c> and current <c>appRoles</c> array before PATCHing.
    /// </summary>
    internal static string GetApplicationsEndpoint(string appId) =>
        $"/v1.0/applications?$filter=appId eq '{Uri.EscapeDataString(appId)}'&$select=id,appId,appRoles";

    /// <summary>
    /// Builds the Graph API URL for a specific Application by its object id. Used by Phase 3
    /// <c>CreateClientRoleAsync</c> to PATCH the updated <c>appRoles</c> array.
    /// </summary>
    internal static string GetApplicationEndpoint(string applicationObjectId) =>
        $"/v1.0/applications/{Uri.EscapeDataString(applicationObjectId)}";

    /// <summary>
    /// Builds the Graph API URL for deleting a single App Role assignment on a user.
    /// </summary>
    internal static string GetUserAppRoleAssignmentEndpoint(string userId, string assignmentId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}/appRoleAssignments/{Uri.EscapeDataString(assignmentId)}";

    // ──── Sessions ────

    /// <summary>
    /// Builds the Graph API URL for revoking all sign-in sessions of a user.
    /// </summary>
    internal static string GetRevokeSessionsEndpoint(string userId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}/revokeSignInSessions";

    // ──── Groups ────

    /// <summary>
    /// Graph API URL for listing all groups.
    /// </summary>
    internal const string GroupsEndpoint = "/v1.0/groups?$select=id,displayName,description";

    /// <summary>
    /// Builds the Graph API URL for listing groups a user is member of.
    /// </summary>
    internal static string GetUserGroupsEndpoint(string userId) =>
        $"/v1.0/users/{Uri.EscapeDataString(userId)}/memberOf/microsoft.graph.group?$select=id,displayName,description";

    /// <summary>
    /// Builds the Graph API URL for adding a member to a group.
    /// </summary>
    internal static string GetGroupMembersRefEndpoint(string groupId) =>
        $"/v1.0/groups/{Uri.EscapeDataString(groupId)}/members/$ref";

    /// <summary>
    /// Builds the Graph API URL for removing a member from a group.
    /// </summary>
    internal static string GetGroupMemberEndpoint(string groupId, string userId) =>
        $"/v1.0/groups/{Uri.EscapeDataString(groupId)}/members/{Uri.EscapeDataString(userId)}/$ref";
}
