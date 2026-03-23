namespace Granit.Bff.ClientAssertion;

/// <summary>
/// Creates <c>private_key_jwt</c> client assertions (RFC 7523) for BFF token endpoint requests.
/// Used when <see cref="Options.BffClientAuthenticationMethod.PrivateKeyJwt"/> is configured.
/// </summary>
public interface IBffClientAssertionService
{
    /// <summary>
    /// Creates a signed JWT assertion for client authentication at the token endpoint.
    /// </summary>
    /// <param name="privateKeyJwk">The client's private key as a JWK JSON string (EC or RSA).</param>
    /// <param name="clientId">The client ID (used as <c>iss</c> and <c>sub</c> claims).</param>
    /// <param name="tokenEndpoint">The token endpoint URL (used as <c>aud</c> claim).</param>
    /// <returns>A signed JWT string suitable for the <c>client_assertion</c> parameter.</returns>
    string CreateAssertion(string privateKeyJwk, string clientId, string tokenEndpoint);
}
