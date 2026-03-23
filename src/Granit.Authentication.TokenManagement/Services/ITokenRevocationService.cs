using Granit.Authentication.Oidc.ClientAuthentication;
using Granit.Authentication.Oidc.Requests;

namespace Granit.Authentication.TokenManagement.Services;

/// <summary>
/// Revokes tokens at an OAuth 2.0 revocation endpoint (RFC 7009).
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>
    /// Revokes a token at the revocation endpoint resolved from the authority's discovery document.
    /// </summary>
    /// <param name="authority">The base URL of the identity provider.</param>
    /// <param name="request">The revocation request containing the token to revoke.</param>
    /// <param name="clientAuth">Optional client authentication strategy to apply to the request.</param>
    /// <param name="dpop">Optional DPoP proof options for sender-constrained tokens.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the server returned a success status code; otherwise <see langword="false"/>.</returns>
    Task<bool> RevokeTokenAsync(
        string authority,
        RevocationRequest request,
        IClientAuthenticationStrategy? clientAuth = null,
        DPoPOptions? dpop = null,
        CancellationToken cancellationToken = default);
}
