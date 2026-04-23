using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Federated.Options;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// HMAC-SHA256 <see cref="IUserLookupHasher"/>. The pepper comes from
/// <see cref="UserCacheHasherOptions.EmailLookupPepper"/> and is validated at
/// startup — an unset pepper fails startup so production deployments cannot
/// accidentally run with a known-zero key.
/// </summary>
internal sealed class HmacUserLookupHasher(IOptions<UserCacheHasherOptions> options) : IUserLookupHasher
{
    private readonly byte[] _pepper = ResolvePepper(options.Value);

    public string? ComputeEmailHash(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        // Canonicalise for case-insensitive exact-match lookup. The same
        // normalisation happens on the search path so the two agree.
        byte[] payload = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        byte[] digest = HMACSHA256.HashData(_pepper, payload);
        return Convert.ToHexStringLower(digest);
    }

    private static byte[] ResolvePepper(UserCacheHasherOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmailLookupPepper))
        {
            throw new InvalidOperationException(
                "UserCacheHasher:EmailLookupPepper is required when at-rest encryption is enabled " +
                "on Granit.Identity.Federated. The pepper must be at least 32 bytes of high-entropy " +
                "random data (e.g. produced by openssl rand -hex 32) and stored separately from the " +
                "encryption key ring so the two can be rotated independently.");
        }

        // Accept either hex or raw UTF-8 — hex if it parses cleanly as pairs of [0-9a-f].
        string pepper = options.EmailLookupPepper.Trim();
        if (pepper.Length % 2 == 0 && pepper.All(IsHexDigit))
        {
            try
            {
                return Convert.FromHexString(pepper);
            }
            catch (FormatException)
            {
                // fall through to UTF-8 interpretation
            }
        }

        return Encoding.UTF8.GetBytes(pepper);
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
