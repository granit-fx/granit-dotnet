using System.Diagnostics;

namespace Granit.Identity.Federated.EntraId.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Identity.EntraId distributed tracing.
/// </summary>
/// <remarks>
/// Register this source in the OpenTelemetry tracer provider (via
/// <c>AddSource(IdentityEntraIdActivitySource.Name)</c>) to capture the Microsoft Graph API
/// spans in Tempo/Grafana.
/// <para>
/// <c>Granit.Observability</c> adds this source automatically when both packages are used.
/// </para>
/// </remarks>
internal static class IdentityEntraIdActivitySource
{
    /// <summary>The name of the Granit.Identity.EntraId <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Identity.EntraId";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string GetUsers = "identity.entraid.get-users";
    internal const string GetUser = "identity.entraid.get-user";
    internal const string GetClients = "identity.entraid.get-clients";
    internal const string GetClientRoles = "identity.entraid.get-client-roles";
    internal const string GetUserClientRoles = "identity.entraid.get-user-client-roles";
    internal const string CreateClientRole = "identity.entraid.create-client-role";
    internal const string AssignClientRole = "identity.entraid.assign-client-role";
    internal const string RemoveClientRole = "identity.entraid.remove-client-role";
    internal const string SetUserEnabled = "identity.entraid.set-user-enabled";
    internal const string UpdateUser = "identity.entraid.update-user";
    internal const string GetUserSessions = "identity.entraid.get-user-sessions";
    internal const string GetUserDeviceActivity = "identity.entraid.get-user-device-activity";
    internal const string GetRoles = "identity.entraid.get-roles";
    internal const string GetRoleMembers = "identity.entraid.get-role-members";
    internal const string GetUserRoles = "identity.entraid.get-user-roles";
    internal const string AssignRole = "identity.entraid.assign-role";
    internal const string RemoveRole = "identity.entraid.remove-role";
    internal const string TerminateSession = "identity.entraid.terminate-session";
    internal const string TerminateAllSessions = "identity.entraid.terminate-all-sessions";
    internal const string CreateUser = "identity.entraid.create-user";
    internal const string GetGroups = "identity.entraid.get-groups";
    internal const string GetUserGroups = "identity.entraid.get-user-groups";
    internal const string AddUserToGroup = "identity.entraid.add-user-to-group";
    internal const string RemoveUserFromGroup = "identity.entraid.remove-user-from-group";

    // ──── Tag names ────

    internal const string TagUserId = "identity.user_id";
    internal const string TagRoleName = "identity.role_name";
    internal const string TagGroupId = "identity.group_id";
    internal const string TagClientId = "identity.client_id";

#pragma warning disable GRSEC003 // Operation name constants, not secrets
    internal const string GetPasswordChangedAt = "identity.entraid.get-password-changed-at";
    internal const string SendPasswordResetEmail = "identity.entraid.send-password-reset-email";
    internal const string SetTemporaryPassword = "identity.entraid.set-temporary-password";
    internal const string VerifyUserCredentials = "identity.entraid.verify-user-credentials";
    internal const string TokenAcquire = "identity.entraid.token-acquire";
#pragma warning restore GRSEC003
}
