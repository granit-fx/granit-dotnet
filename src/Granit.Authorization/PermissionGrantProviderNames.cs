namespace Granit.Authorization;

/// <summary>
/// Canonical provider names identifying the grantee of a <see cref="Domain.PermissionGrant"/>.
/// Kept short for compact storage — single-char letters keep indexes compact.
/// </summary>
/// <remarks>
/// Extend this type with additional providers (e.g. <c>"O"</c> for organization, <c>"G"</c> for
/// AD group) as custom <see cref="IPermissionGrantValidator"/> and grant manager extensions
/// evolve.
/// </remarks>
public static class PermissionGrantProviderNames
{
    /// <summary>Provider name for role-based grants: <c>ProviderKey</c> holds the role name.</summary>
    public const string Role = "R";

    /// <summary>Provider name for user-based grants: <c>ProviderKey</c> holds the user's <c>sub</c> (GUID or opaque id).</summary>
    public const string User = "U";

    /// <summary>Provider name for OIDC client-based grants: <c>ProviderKey</c> holds the <c>client_id</c>.</summary>
    public const string Client = "C";
}
