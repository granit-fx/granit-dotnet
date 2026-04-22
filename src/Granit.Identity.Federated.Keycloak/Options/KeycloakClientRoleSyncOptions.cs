namespace Granit.Identity.Federated.Keycloak.Options;

/// <summary>
/// Configuration for the Keycloak client-role sync pipeline. Populates
/// <c>RoleMetadata.ClientId</c> with the client-scope roles declared on the listed
/// Keycloak clients — so grants can target them and <c>IGranitRoleLookup.FindByNameAsync</c>
/// can distinguish realm roles from client-scope roles.
/// </summary>
public sealed class KeycloakClientRoleSyncOptions
{
    /// <summary>Configuration section: <c>KeycloakAdmin:ClientRoleSync</c>.</summary>
    public const string SectionName = "KeycloakAdmin:ClientRoleSync";

    /// <summary>
    /// When <see langword="false"/>, the sync contributor becomes a no-op even if tracked
    /// client ids are configured. Default: <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OIDC client ids (as seen by Keycloak — the <c>clientId</c> string, NOT the internal
    /// UUID) whose client-scope roles should be mirrored into <c>RoleMetadata</c>. The sync
    /// is opt-in by design: enumerating every Keycloak client would surface internal
    /// clients (<c>admin-cli</c>, <c>security-admin-console</c>, <c>account</c>, etc.) that
    /// have no application relevance.
    /// </summary>
    public IReadOnlyList<string> TrackedClientIds { get; set; } = [];
}
