using System.Security.Cryptography;
using System.Text;

namespace Granit.Authentication.Oidc.Internal;

/// <summary>
/// Internal helper for manual JWT construction without dependency on Microsoft.IdentityModel.
/// Supports EC (ES256) and RSA (PS256) signing algorithms.
/// </summary>
internal static class JwtBuilder
{
    /// <summary>
    /// Creates a JWT signed with ECDSA using P-256 and SHA-256 (ES256).
    /// </summary>
    /// <param name="headerJson">The JSON string for the JWT header.</param>
    /// <param name="payloadJson">The JSON string for the JWT payload.</param>
    /// <param name="ecdsa">The ECDSA key to sign with.</param>
    /// <returns>A compact-serialized JWT string.</returns>
    internal static string CreateEcJwt(string headerJson, string payloadJson, ECDsa ecdsa)
    {
        string headerB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(headerJson));
        string payloadB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));
        string signingInput = $"{headerB64}.{payloadB64}";

        byte[] signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    /// <summary>
    /// Creates a JWT signed with RSA-PSS using SHA-256 (PS256).
    /// </summary>
    /// <param name="headerJson">The JSON string for the JWT header.</param>
    /// <param name="payloadJson">The JSON string for the JWT payload.</param>
    /// <param name="rsa">The RSA key to sign with.</param>
    /// <returns>A compact-serialized JWT string.</returns>
    internal static string CreateRsaJwt(string headerJson, string payloadJson, RSA rsa)
    {
        string headerB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(headerJson));
        string payloadB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));
        string signingInput = $"{headerB64}.{payloadB64}";

        byte[] signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }
}
