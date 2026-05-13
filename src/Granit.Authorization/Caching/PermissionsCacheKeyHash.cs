using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Authorization.Caching;

/// <summary>
/// Stable 16-hex-char SHA-256 over a <see cref="ClaimsPrincipal"/>'s role + permission claims
/// (plus <c>sub</c>). Used as a partition key in cache keys so two callers with different
/// RBAC snapshots don't share each other's cached filtered payloads.
/// </summary>
/// <remarks>
/// 16 hex chars is sufficient: collisions only mean a stale entry may be served until the
/// next TTL boundary; security gating still runs on every request — this is a partition
/// hint, not an authorization decision.
/// <para>
/// Considered claim types:
/// <list type="bullet">
///   <item><see cref="ClaimTypes.Role"/></item>
///   <item><c>"role"</c> (short form, common in JWT)</item>
///   <item><c>"permission"</c></item>
/// </list>
/// Subject identity is taken from <see cref="ClaimTypes.NameIdentifier"/> or the JWT
/// <c>sub</c> claim. Anonymous principals hash as <c>"anon"</c>.
/// </para>
/// </remarks>
public static class PermissionsCacheKeyHash
{
    /// <summary>
    /// Computes the cache-partition hash for <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The principal whose RBAC snapshot should be hashed.</param>
    /// <returns>A 16-character lowercase hex string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
    public static string Compute(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        IEnumerable<string> roleClaims = user.Claims
            .Where(c => c.Type is ClaimTypes.Role or "role" or "permission")
            .Select(c => $"{c.Type}={c.Value}")
            .OrderBy(s => s, StringComparer.Ordinal);

        StringBuilder buffer = new();
        foreach (string claim in roleClaims)
        {
            buffer.Append(claim);
            buffer.Append('|');
        }

        string? sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        buffer.Append(sub ?? "anon");

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString()));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
