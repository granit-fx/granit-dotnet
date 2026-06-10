using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Granit.Authentication.DPoP.Validation.Internal;

/// <summary>
/// Computes JWK Thumbprints per RFC 7638.
/// The thumbprint is the base64url-encoded SHA-256 hash of the canonical JSON
/// representation of the public key, with members in lexicographic order.
/// </summary>
internal static class JwkThumbprintCalculator
{
    /// <summary>
    /// Computes the JWK Thumbprint (RFC 7638) of a JWK JSON string.
    /// </summary>
    /// <param name="jwk">The JWK as a JSON element (may contain private key params — they are stripped).</param>
    /// <returns>The base64url-encoded SHA-256 thumbprint.</returns>
    internal static string ComputeThumbprint(JsonElement jwk)
    {
        string kty = jwk.GetProperty("kty").GetString()!;

        // Build canonical JSON with REQUIRED members in LEXICOGRAPHIC order (RFC 7638 §3.2)
        // No whitespace, no optional members, no private key parameters
        byte[] canonicalJson = kty switch
        {
            "EC" => BuildEcCanonical(jwk),
            "RSA" => BuildRsaCanonical(jwk),
            _ => throw new NotSupportedException($"Unsupported key type '{kty}' for JWK Thumbprint."),
        };

        byte[] hash = SHA256.HashData(canonicalJson);
        return System.Buffers.Text.Base64Url.EncodeToString(hash);
    }

    /// <summary>
    /// EC canonical form: {"crv":"...","kty":"EC","x":"...","y":"..."}
    /// Members in lexicographic order: crv, kty, x, y
    /// </summary>
    private static byte[] BuildEcCanonical(JsonElement jwk)
    {
        string crv = jwk.GetProperty("crv").GetString()!;
        string x = jwk.GetProperty("x").GetString()!;
        string y = jwk.GetProperty("y").GetString()!;

        ArrayBufferWriter<byte> buffer = new(256);
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WriteString("crv", crv);
        writer.WriteString("kty", "EC");
        writer.WriteString("x", x);
        writer.WriteString("y", y);
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// RSA canonical form: {"e":"...","kty":"RSA","n":"..."}
    /// Members in lexicographic order: e, kty, n
    /// </summary>
    private static byte[] BuildRsaCanonical(JsonElement jwk)
    {
        string e = jwk.GetProperty("e").GetString()!;
        string n = jwk.GetProperty("n").GetString()!;

        ArrayBufferWriter<byte> buffer = new(4096);
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WriteString("e", e);
        writer.WriteString("kty", "RSA");
        writer.WriteString("n", n);
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }
}
