using Granit.Authorization;

namespace Granit.Identity.Federated.Cognito.Options;

/// <summary>
/// Configuration for the Cognito app-client group-sync pipeline. Populates
/// <c>RoleMetadata.ClientId</c> with Cognito groups whose names follow the
/// <c>{appClientId}{Delimiter}{roleName}</c> naming convention — so grants can target
/// them and <c>IGranitRoleLookup.FindByNameAsync</c> can distinguish realm-style roles
/// (un-prefixed groups) from app-scope roles (prefixed groups).
/// </summary>
/// <remarks>
/// <para>
/// AWS Cognito has no native binding between groups and User Pool app clients: groups
/// are a flat list within a User Pool. Granit therefore relies on a <b>naming prefix</b>
/// convention to scope groups to a specific app client — see ADR-027.
/// </para>
/// <para>
/// <b>Strict-by-design:</b> only groups matching a tracked-client prefix are synced here.
/// Un-prefixed groups keep flowing through the existing realm-role path
/// (<c>IIdentityRoleManager.GetRolesAsync</c> + role orchestrator) and surface as
/// <c>ClientId = null</c> — no double-write.
/// </para>
/// </remarks>
public sealed class CognitoClientRoleSyncOptions
{
    /// <summary>Configuration section: <c>CognitoAdmin:ClientRoleSync</c>.</summary>
    public const string SectionName = "CognitoAdmin:ClientRoleSync";

    /// <summary>
    /// When <see langword="false"/>, the sync contributor becomes a no-op even if
    /// tracked app-client ids are configured. Default: <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cognito User Pool app-client IDs whose prefix-matching groups should be mirrored
    /// into <c>RoleMetadata</c> with <c>ClientId</c> set. Opt-in — the sync never walks
    /// groups for un-tracked clients.
    /// </summary>
    public IReadOnlyList<string> TrackedAppClientIds { get; set; } = [];

    /// <summary>
    /// Character that separates the app-client id from the role name inside a group
    /// name. A group called <c>{TrackedAppClientIds[0]}{Delimiter}editor</c> becomes
    /// an <c>IdentityRole("editor") { ClientId = TrackedAppClientIds[0] }</c>.
    /// Default: <c>":"</c>.
    /// </summary>
    /// <remarks>
    /// Cognito group names accept Unicode letters, marks, symbols, numbers, and
    /// punctuation — so colons, hyphens, underscores, etc. are all legal. Override
    /// when an existing deployment already uses a different separator.
    /// </remarks>
    public string Delimiter { get; set; } = ":";

    /// <summary>
    /// Policy applied to <see cref="Granit.Authorization.Domain.RoleMetadata"/> rows whose
    /// upstream Cognito group disappears between sync runs (deleted from the User Pool or
    /// renamed out of the tracked prefix). See ADR-029. Default:
    /// <see cref="OrphanedRolePolicy.KeepAndLog"/> — preserves the Phase 2 behaviour.
    /// </summary>
    public OrphanedRolePolicy OrphanedRolePolicy { get; set; } = OrphanedRolePolicy.KeepAndLog;
}
