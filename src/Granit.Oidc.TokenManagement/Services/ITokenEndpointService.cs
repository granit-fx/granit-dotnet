using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;

namespace Granit.Oidc.TokenManagement.Services;

/// <summary>
/// DPoP proof options for token endpoint requests (RFC 9449).
/// </summary>
/// <param name="PrivateKeyJwk">The DPoP private key in JWK format.</param>
/// <param name="Nonce">Optional server-provided nonce for replay protection.</param>
public sealed record DPoPOptions(string PrivateKeyJwk, string? Nonce = null);

/// <summary>
/// Sends requests to an OAuth 2.0 token endpoint, handling discovery resolution,
/// client authentication, and DPoP proof generation.
/// </summary>
public interface ITokenEndpointService
{
    /// <summary>
    /// Sends a token request to the token endpoint resolved from the authority's discovery document.
    /// </summary>
    /// <param name="authority">The base URL of the identity provider.</param>
    /// <param name="request">The typed token request (authorization code, refresh, or client credentials).</param>
    /// <param name="clientAuth">Optional client authentication strategy to apply to the request.</param>
    /// <param name="dpop">Optional DPoP proof options for sender-constrained tokens.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The parsed <see cref="TokenResponse"/> from the token endpoint.</returns>
    Task<TokenResponse> RequestTokenAsync(
        string authority,
        TokenRequest request,
        IClientAuthenticationStrategy? clientAuth = null,
        DPoPOptions? dpop = null,
        CancellationToken cancellationToken = default);
}
