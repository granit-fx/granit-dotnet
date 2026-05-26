using System.Security.Cryptography;
using System.Text;

namespace Granit.Indexing.Internal;

/// <summary>
/// One-way hash of the principal identifier so the raw value never reaches the rate-
/// limiter's in-memory map, log emission, or telemetry — limits the blast radius of an
/// accidental dump and removes a low-effort PII trail.
/// </summary>
/// <remarks>
/// SHA-256 truncated to 16 hex chars (64 bits of entropy) — collision-resistant enough
/// for a per-process trailing-minute bucket key but compact in memory and logs.
/// </remarks>
internal static class PrincipalIdentifierHasher
{
    public static string Hash(string principalIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(principalIdentifier);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(principalIdentifier));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
