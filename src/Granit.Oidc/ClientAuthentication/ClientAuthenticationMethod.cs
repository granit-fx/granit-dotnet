namespace Granit.Oidc.ClientAuthentication;

/// <summary>
/// Specifies the method used for client authentication at the token endpoint.
/// </summary>
public enum ClientAuthenticationMethod
{
    /// <summary>
    /// Client secret included as a POST body parameter (<c>client_secret_post</c>).
    /// </summary>
    ClientSecretPost,

    /// <summary>
    /// Client authenticates using a signed JWT assertion (<c>private_key_jwt</c>, RFC 7523).
    /// </summary>
    PrivateKeyJwt,

    /// <summary>
    /// Client secret sent via HTTP Basic authentication (<c>client_secret_basic</c>).
    /// </summary>
    ClientSecretBasic,
}
