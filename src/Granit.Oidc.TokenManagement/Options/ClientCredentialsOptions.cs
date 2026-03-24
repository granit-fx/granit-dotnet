using Granit.Oidc.ClientAuthentication;

#pragma warning disable GRSEC003 // Options class stores client secrets and signing keys for OAuth 2.0 client credentials flow

namespace Granit.Oidc.TokenManagement.Options;

/// <summary>
/// Options for a named client credentials grant configuration.
/// Bound per client name via <c>IOptionsMonitor&lt;ClientCredentialsOptions&gt;</c>.
/// </summary>
public sealed class ClientCredentialsOptions
{
    /// <summary>
    /// The base URL of the identity provider (e.g., <c>"https://idp.example.com"</c>).
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth 2.0 client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The client secret for <see cref="ClientAuthenticationMethod.ClientSecretPost"/> authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The scope(s) to request (space-delimited).
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// The client authentication method to use at the token endpoint.
    /// Defaults to <see cref="ClientAuthenticationMethod.ClientSecretPost"/>.
    /// </summary>
    public ClientAuthenticationMethod ClientAuthenticationMethod { get; set; } = ClientAuthenticationMethod.ClientSecretPost;

    /// <summary>
    /// The private signing key in JWK format for <see cref="ClientAuthenticationMethod.PrivateKeyJwt"/> authentication.
    /// </summary>
    public string? ClientSigningKeyJwk { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the client uses DPoP (RFC 9449) to bind tokens to a proof key.
    /// </summary>
    public bool UseDPoP { get; set; }

    /// <summary>
    /// Optional per-client override for the cache margin subtracted from token lifetime.
    /// When <see langword="null"/>, <see cref="TokenManagementOptions.DefaultCacheMargin"/> is used.
    /// </summary>
    public TimeSpan? CacheMargin { get; set; }
}

#pragma warning restore GRSEC003
