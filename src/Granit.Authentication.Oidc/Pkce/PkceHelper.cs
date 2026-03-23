using System.Security.Cryptography;
using Granit.Authentication.Oidc.Internal;

namespace Granit.Authentication.Oidc.Pkce;

/// <summary>
/// PKCE (Proof Key for Code Exchange) helper for generating code verifiers and challenges (RFC 7636).
/// </summary>
public static class PkceHelper
{
    private const int CodeVerifierLength = 64;

    /// <summary>
    /// Generates a cryptographically random code verifier string (base64url-encoded, 64 bytes).
    /// </summary>
    /// <returns>A base64url-encoded code verifier suitable for the <c>code_verifier</c> parameter.</returns>
    public static string GenerateCodeVerifier()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(CodeVerifierLength);
        return Base64Url.Encode(bytes);
    }

    /// <summary>
    /// Computes the S256 code challenge for the given code verifier.
    /// </summary>
    /// <param name="codeVerifier">The code verifier to hash.</param>
    /// <returns>A base64url-encoded SHA-256 hash suitable for the <c>code_challenge</c> parameter.</returns>
    public static string ComputeCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);

        byte[] hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return Base64Url.Encode(hash);
    }
}
