namespace Granit.Identity.Endpoints.Workspaces;

/// <summary>
/// Feature name constants for the identity module (per ADR-057).
/// Mirrors the <see cref="Permissions.IdentityPermissions"/> pattern so
/// hosts compose with <c>section.Feature(IdentityFeatures.Users)</c>
/// instead of typing the string by hand.
/// </summary>
public static class IdentityFeatures
{
    /// <summary>Users list — paired with <c>/identity/users</c> on the React shell.</summary>
    public const string Users = "identity.users";

    /// <summary>Roles list — paired with <c>/identity/roles</c>.</summary>
    public const string Roles = "identity.roles";

    /// <summary>Groups list — paired with <c>/identity/groups</c>.</summary>
    public const string Groups = "identity.groups";

    /// <summary>Sessions list — paired with <c>/identity/sessions</c>.</summary>
    public const string Sessions = "identity.sessions";
}
