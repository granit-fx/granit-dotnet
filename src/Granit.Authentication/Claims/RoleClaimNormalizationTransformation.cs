using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.Claims;

/// <summary>
/// Generic claims transformation that copies role values stored under non-standard claim
/// types onto <see cref="ClaimTypes.Role"/>, restoring the framework's
/// "<c>roles live at <see cref="ClaimTypes.Role"/></c>" contract for authentication
/// schemes that emit roles under OIDC short names (e.g. <c>"role"</c>).
/// </summary>
/// <remarks>
/// <para>
/// Configure via <see cref="RoleClaimNormalizationOptions"/>:
/// </para>
/// <list type="bullet">
///   <item><see cref="RoleClaimNormalizationOptions.Schemes"/> — only principals whose
///   <see cref="ClaimsIdentity.AuthenticationType"/> matches one of these scheme names
///   are normalized. Empty = transformation is a no-op (safe default — never silently
///   mutates principals).</item>
///   <item><see cref="RoleClaimNormalizationOptions.SourceClaimTypes"/> — claim types
///   whose values get copied to <see cref="ClaimTypes.Role"/>. Defaults to
///   <c>["role"]</c> (RFC 8693 / OIDC short claim).</item>
/// </list>
/// <para>
/// Idempotent: re-running the transformation on a principal that already has the
/// normalized claims does not produce duplicates.
/// </para>
/// </remarks>
public sealed class RoleClaimNormalizationTransformation(
    IOptions<RoleClaimNormalizationOptions> options) : IClaimsTransformation
{
    private readonly RoleClaimNormalizationOptions _options = options.Value;

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        if (_options.Schemes.Count == 0 || !_options.Schemes.Contains(
                identity.AuthenticationType ?? string.Empty,
                StringComparer.Ordinal))
        {
            return Task.FromResult(principal);
        }

        HashSet<string> existing = [.. identity.FindAll(ClaimTypes.Role).Select(c => c.Value)];

        // Materialize source claims before mutating: ClaimsIdentity.FindAll returns a
        // live enumerator over the underlying claims collection — calling AddClaim
        // mid-iteration throws InvalidOperationException.
        List<Claim> sourceClaims = [];
        foreach (string sourceType in _options.SourceClaimTypes)
        {
            if (string.Equals(sourceType, ClaimTypes.Role, StringComparison.Ordinal))
            {
                continue;
            }

            sourceClaims.AddRange(identity.FindAll(sourceType));
        }

        foreach (Claim claim in sourceClaims)
        {
            if (existing.Add(claim.Value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, claim.Value));
            }
        }

        return Task.FromResult(principal);
    }
}
