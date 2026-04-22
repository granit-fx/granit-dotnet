namespace Granit.Identity.Federated.EntraId.Options;

/// <summary>
/// Configuration for the Entra ID app-role sync pipeline. Populates
/// <c>RoleMetadata.ClientId</c> with the App Roles declared on the listed
/// applications — so grants can target them and <c>IGranitRoleLookup.FindByNameAsync</c>
/// can distinguish realm-style roles from app-scope roles.
/// </summary>
public sealed class EntraIdClientRoleSyncOptions
{
    /// <summary>Configuration section: <c>EntraIdAdmin:ClientRoleSync</c>.</summary>
    public const string SectionName = "EntraIdAdmin:ClientRoleSync";

    /// <summary>
    /// When <see langword="false"/>, the sync contributor becomes a no-op even if tracked
    /// app ids are configured. Default: <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OIDC <c>appId</c>s (GUID strings, as seen in the application registration) whose
    /// App Roles should be mirrored into <c>RoleMetadata</c>. Opt-in: the sync never
    /// enumerates every Service Principal in the tenant — Entra has thousands of them
    /// (Microsoft's built-in apps, other SaaS integrations) that have no relevance here.
    /// </summary>
    public IReadOnlyList<string> TrackedAppIds { get; set; } = [];
}
