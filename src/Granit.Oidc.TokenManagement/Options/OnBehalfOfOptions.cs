using Granit.Oidc.ClientAuthentication;

#pragma warning disable GRSEC003 // Options class stores client secrets and signing keys for OAuth 2.0 token exchange

namespace Granit.Oidc.TokenManagement.Options;

/// <summary>
/// Options for a named OAuth 2.0 Token Exchange (RFC 8693) client configuration,
/// used for act-on-behalf-of (OBO) calls to downstream services.
/// </summary>
/// <remarks>
/// <para>
/// The handler exchanges the inbound user access token for a new token scoped
/// to <see cref="Audience"/> with scopes limited to <see cref="Scopes"/>,
/// optionally binding the result with DPoP (RFC 9449) via
/// <see cref="RequireDPoP"/>. This replaces naive bearer-token propagation,
/// which exposes the caller's original token to every downstream host and
/// cannot be audience-restricted.
/// </para>
/// <para>
/// Bound per client name via <c>IOptionsMonitor&lt;OnBehalfOfOptions&gt;</c>.
/// </para>
/// </remarks>
public sealed class OnBehalfOfOptions
{
    /// <summary>
    /// Base URL of the identity provider (e.g. <c>"https://idp.example.com"</c>).
    /// Discovery is performed against <c>{Authority}/.well-known/openid-configuration</c>.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth 2.0 client identifier of THIS service (not the caller's client).
    /// The IdP uses it to authenticate the token-exchange request.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for <see cref="ClientAuthenticationMethod.ClientSecretPost"/>.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Client authentication method used at the token endpoint.
    /// Defaults to <see cref="ClientAuthenticationMethod.ClientSecretPost"/>.
    /// </summary>
    public ClientAuthenticationMethod ClientAuthenticationMethod { get; set; } = ClientAuthenticationMethod.ClientSecretPost;

    /// <summary>
    /// Private signing key (JWK) for <see cref="ClientAuthenticationMethod.PrivateKeyJwt"/>.
    /// </summary>
    public string? ClientSigningKeyJwk { get; set; }

    /// <summary>
    /// Target audience for the exchanged token — typically the downstream API's
    /// client id or resource indicator. Mandatory: without it the exchanged
    /// token's <c>aud</c> is whatever the IdP defaults to, which defeats the
    /// confused-deputy mitigation.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Scopes requested for the exchanged token. Narrow these to the minimum
    /// required by the downstream API; the IdP may also apply its own policy.
    /// </summary>
    public IList<string> Scopes { get; set; } = [];

    /// <summary>
    /// When <see langword="true"/> (default), the handler binds the exchanged
    /// token to a per-process DPoP key (RFC 9449) and attaches a fresh
    /// <c>DPoP</c> proof to every outbound request. Strongly recommended for
    /// inter-service calls so a stolen token is unusable without the proof
    /// key. Only set to <see langword="false"/> for IdPs that do not support
    /// DPoP.
    /// </summary>
    public bool RequireDPoP { get; set; } = true;

    /// <summary>
    /// Allow-list of downstream hostnames this client may propagate auth to.
    /// Case-insensitive exact match against <c>HttpRequestMessage.RequestUri.Host</c>.
    /// Mandatory: without this list the handler refuses to attach tokens, which
    /// avoids the SSRF-style exfiltration vector of the previous
    /// <c>AuthTokenPropagationHandler</c>.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    /// <summary>
    /// Safety margin subtracted from the issued token's lifetime before it is
    /// evicted from the cache. Prevents using a token that is about to expire
    /// mid-request.
    /// </summary>
    public TimeSpan TokenLifetimeSafetyMargin { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When <see langword="true"/>, refuses non-https request targets even if
    /// their host is in <see cref="AllowedHosts"/>. Keep enabled in production;
    /// may be disabled in test environments that use http://localhost.
    /// </summary>
    public bool RequireHttps { get; set; } = true;
}

#pragma warning restore GRSEC003
