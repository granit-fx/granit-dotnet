using System.Security.Cryptography;
using System.Text;
using Granit.Parties.EntityFrameworkCore.Options;
using Microsoft.Extensions.Options;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>
/// HMAC-SHA256 <see cref="IPartyLookupHasher"/>. Pepper sourced from
/// <see cref="PartyLookupHasherOptions.Pepper"/> and validated at startup —
/// an unset pepper fails fast so production deployments cannot accidentally
/// run with a known-zero key.
/// </summary>
internal sealed class HmacPartyLookupHasher(IOptions<PartyLookupHasherOptions> options)
    : IPartyLookupHasher
{
    private readonly byte[] _pepper = ResolvePepper(options.Value);

    public string? ComputeHash(string? canonical)
    {
        if (string.IsNullOrEmpty(canonical))
        {
            return null;
        }

        // Canonical inputs are already canonicalised by EmailCanonicaliser /
        // PhoneCanonicaliser (lower-case, separator-stripped) — no further
        // normalisation here would change the hash.
        byte[] payload = Encoding.UTF8.GetBytes(canonical);
        byte[] digest = HMACSHA256.HashData(_pepper, payload);
        return Convert.ToHexStringLower(digest);
    }

    private static byte[] ResolvePepper(PartyLookupHasherOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Pepper))
        {
            throw new InvalidOperationException(
                "Parties:LookupHasher:Pepper is required when at-rest encryption is enabled " +
                "on Granit.Parties. The pepper must be at least 32 bytes of high-entropy " +
                "random data (e.g. openssl rand -hex 32) and stored separately from the " +
                "encryption key ring so the two can be rotated independently.");
        }

        string pepper = options.Pepper.Trim();
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
