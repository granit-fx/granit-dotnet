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

    /// <summary>
    /// Provider name for user-based grants: <c>ProviderKey</c> holds the
    /// canonical <see cref="Granit.Identity.Domain.User.Id"/> (Guid string)
    /// per ADR-051 B-step 4. The same Guid resolves both the local-side
    /// <c>LocalIdentity</c> and the federated-side <c>FederatedIdentity</c>
    /// rows when the alignment guarantee holds, so a single grant covers
    /// any login path the user takes.
    /// </summary>
    public const string User = "U";

    /// <summary>Provider name for OIDC client-based grants: <c>ProviderKey</c> holds the <c>client_id</c>.</summary>
    public const string Client = "C";
}
