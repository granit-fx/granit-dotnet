using System.Diagnostics;

namespace Granit.Identity.Federated.Keycloak.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Identity.Keycloak distributed tracing.
/// </summary>
/// <remarks>
/// Register this source in the OpenTelemetry tracer provider (via
/// <c>AddSource(IdentityKeycloakActivitySource.Name)</c>) to capture the Keycloak Admin API
/// spans in Tempo/Grafana.
/// <para>
/// <c>Granit.Observability</c> adds this source automatically when both packages are used.
/// </para>
/// </remarks>
internal static class IdentityKeycloakActivitySource
{
    /// <summary>The name of the Granit.Identity.Keycloak <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Identity.Keycloak";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string GetUsers = "identity.keycloak.get-users";
    internal const string GetUser = "identity.keycloak.get-user";
    internal const string GetClients = "identity.keycloak.get-clients";
    internal const string GetClientRoles = "identity.keycloak.get-client-roles";
    internal const string GetUserClientRoles = "identity.keycloak.get-user-client-roles";
    internal const string CreateClientRole = "identity.keycloak.create-client-role";
    internal const string AssignClientRole = "identity.keycloak.assign-client-role";
    internal const string RemoveClientRole = "identity.keycloak.remove-client-role";
    internal const string SetUserEnabled = "identity.keycloak.set-user-enabled";
    internal const string UpdateUser = "identity.keycloak.update-user";
    internal const string GetUserSessions = "identity.keycloak.get-user-sessions";
    internal const string GetUserDeviceActivity = "identity.keycloak.get-user-device-activity";
    internal const string GetRoles = "identity.keycloak.get-roles";
    internal const string GetRoleMembers = "identity.keycloak.get-role-members";
    internal const string GetUserRoles = "identity.keycloak.get-user-roles";
    internal const string AssignRole = "identity.keycloak.assign-role";
    internal const string RemoveRole = "identity.keycloak.remove-role";
    internal const string TerminateSession = "identity.keycloak.terminate-session";
    internal const string TerminateAllSessions = "identity.keycloak.terminate-all-sessions";
    internal const string CreateUser = "identity.keycloak.create-user";
    internal const string GetGroups = "identity.keycloak.get-groups";
    internal const string GetUserGroups = "identity.keycloak.get-user-groups";
    internal const string AddUserToGroup = "identity.keycloak.add-user-to-group";
    internal const string RemoveUserFromGroup = "identity.keycloak.remove-user-from-group";

    // ──── Tag names ────

    internal const string TagUserId = "identity.keycloak.user_id";
    internal const string TagRoleName = "identity.keycloak.role_name";
    internal const string TagClientId = "identity.keycloak.client_id";
    internal const string TagGroupId = "identity.keycloak.group_id";
    internal const string TagEnabled = "identity.keycloak.enabled";
    internal const string TagSearch = "identity.keycloak.search";

#pragma warning disable GRSEC003 // Operation name constants, not secrets
    internal const string GetPasswordChangedAt = "identity.keycloak.get-password-changed-at";
    internal const string SendPasswordResetEmail = "identity.keycloak.send-password-reset-email";
    internal const string SetTemporaryPassword = "identity.keycloak.set-temporary-password";
    internal const string VerifyUserCredentials = "identity.keycloak.verify-user-credentials";
    internal const string TokenAcquire = "identity.keycloak.token-acquire";
#pragma warning restore GRSEC003
}
