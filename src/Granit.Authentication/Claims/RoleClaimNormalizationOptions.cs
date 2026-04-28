using System.Security.Claims;

namespace Granit.Authentication.Claims;

/// <summary>
/// Configuration for <see cref="RoleClaimNormalizationTransformation"/>.
/// </summary>
public sealed class RoleClaimNormalizationOptions
{
    /// <summary>
    /// Authentication scheme names whose principals should be normalized. Empty list
    /// means the transformation is a no-op for every principal (safe default).
    /// Multiple schemes are supported.
    /// </summary>
    public IList<string> Schemes { get; } = [];

    /// <summary>
    /// Claim types whose values are copied onto <see cref="ClaimTypes.Role"/>.
    /// Defaults to <c>["role"]</c> — the OIDC short claim per RFC 8693, emitted by
    /// OpenIddict.Validation, IdentityServer, Auth0, and most generic OIDC stacks.
    /// </summary>
    public IList<string> SourceClaimTypes { get; set; } = ["role"];
}
