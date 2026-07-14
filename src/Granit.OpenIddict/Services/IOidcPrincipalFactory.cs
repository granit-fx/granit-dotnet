using System.Collections.Immutable;
using System.Security.Claims;
using Granit.Identity.Local.Domain;

namespace Granit.OpenIddict.Services;

/// <summary>
/// Builds a <see cref="ClaimsPrincipal"/> for OIDC token issuance from a
/// <see cref="LocalIdentity"/> (user flows) or a client id (client-credentials flow).
/// </summary>
/// <remarks>
/// Lives in the base module (no HTTP dependency) so the OIDC protocol endpoints, custom grants,
/// and any non-HTTP token-issuance path share one claim-mapping implementation.
/// </remarks>
public interface IOidcPrincipalFactory
{
    /// <summary>
    /// Creates a principal with standard OIDC claims from the user, sets the requested scopes,
    /// and applies claim destinations.
    /// </summary>
    Task<ClaimsPrincipal> CreateUserPrincipalAsync(
        LocalIdentity user,
        ImmutableArray<string> scopes,
        string authenticationScheme,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a principal for the client-credentials flow (no user).
    /// </summary>
    ClaimsPrincipal CreateClientPrincipal(
        string clientId,
        ImmutableArray<string> scopes,
        string authenticationScheme);
}
