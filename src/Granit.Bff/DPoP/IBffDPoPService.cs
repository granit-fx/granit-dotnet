namespace Granit.Bff.DPoP;

/// <summary>
/// Generates DPoP (Demonstrating Proof-of-Possession, RFC 9449) key pairs and proof JWTs.
/// Used by the BFF to bind tokens to a cryptographic key, preventing replay attacks.
/// </summary>
public interface IBffDPoPService
{
    /// <summary>
    /// Generates an EC P-256 key pair and returns the private key as a JWK JSON string.
    /// The key pair is used for the lifetime of a BFF session.
    /// </summary>
    /// <returns>A JWK JSON string containing the private key parameters.</returns>
    string GenerateKeyPair();

    /// <summary>
    /// Creates a DPoP proof JWT signed with the given private key.
    /// </summary>
    /// <param name="privateKeyJwk">The private key as a JWK JSON string (from <see cref="GenerateKeyPair"/>).</param>
    /// <param name="httpMethod">The HTTP method of the request (e.g., <c>"POST"</c>, <c>"GET"</c>).</param>
    /// <param name="httpUri">The full URL of the target endpoint (scheme + host + path, no query).</param>
    /// <returns>A signed DPoP proof JWT string.</returns>
    string CreateProof(string privateKeyJwk, string httpMethod, string httpUri);
}
