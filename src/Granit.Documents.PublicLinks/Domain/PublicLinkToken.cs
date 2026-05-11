using System.Security.Cryptography;
using System.Text;
using Granit.Domain;

namespace Granit.Documents.PublicLinks.Domain;

/// <summary>
/// Strongly typed wrapper around the bearer-token string exchanged with end users.
/// </summary>
/// <remarks>
/// Tokens are 32 cryptographically-random bytes encoded as URL-safe base64
/// (no padding, length 43). They are returned exactly once at creation time —
/// only the HMAC digest is persisted.
/// </remarks>
public sealed class PublicLinkToken : SingleValueObject<string>
{
    /// <summary>Number of random bytes in a freshly minted token.</summary>
    public const int RandomByteCount = 32;

    /// <summary>Expected length, in characters, of the URL-safe base64 encoding.</summary>
    public const int EncodedLength = 43;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>Creates a token wrapper from an already-formatted string.</summary>
    public static PublicLinkToken Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PublicLinkToken { Value = value };
    }

    /// <summary>Implicit unwrap to <see cref="string"/>.</summary>
    public static implicit operator string(PublicLinkToken token) =>
        token is null ? string.Empty : token.Value;
}

/// <summary>
/// Generates random bearer tokens and computes their HMAC-SHA256 digest using
/// the host's signing key (a pepper, sourced from Vault and held in
/// <c>GranitDocumentsPublicLinksOptions.SigningKey</c>).
/// </summary>
public static class PublicLinkTokenFactory
{
    /// <summary>Mints a fresh random token (32 bytes, URL-safe base64, no padding).</summary>
    public static PublicLinkToken GenerateRandom()
    {
        Span<byte> buffer = stackalloc byte[PublicLinkToken.RandomByteCount];
        RandomNumberGenerator.Fill(buffer);
        string encoded = Base64Url.Encode(buffer);
        return PublicLinkToken.Create(encoded);
    }

    /// <summary>
    /// Computes the HMAC-SHA256 digest of <paramref name="token"/> using
    /// <paramref name="pepper"/>. Deterministic for a given (token, pepper) pair.
    /// </summary>
    public static byte[] ComputeHash(string token, byte[] pepper)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(pepper);
        if (pepper.Length == 0)
        {
            throw new ArgumentException("Signing key (pepper) must not be empty.", nameof(pepper));
        }
        byte[] payload = Encoding.UTF8.GetBytes(token);
        return HMACSHA256.HashData(pepper, payload);
    }
}

internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        string standard = Convert.ToBase64String(bytes);
        // RFC 4648 §5: replace + / and strip padding.
        return standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
