using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Workspaces.Endpoints.Internal;

/// <summary>
/// Builds FusionCache keys for the workspace tree endpoint. Same shape as
/// <c>EntityCacheKey</c> in <c>Granit.Entities.Endpoints</c> — embeds the
/// caller's permission hash so partitioned RBAC snapshots cannot bleed across
/// users with the same tenant + culture.
/// </summary>
internal static class WorkspaceCacheKey
{
    public const string Prefix = "workspaces";

    public static string Build(ClaimsPrincipal user, CultureInfo culture, bool includeShells)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(culture);

        return $"{Prefix}:{HashPermissions(user)}:{culture.Name}:{(includeShells ? 1 : 0)}";
    }

    private static string HashPermissions(ClaimsPrincipal user)
    {
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
