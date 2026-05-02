using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Cache-key composer for the calendar range endpoint (story #1691).
/// Format: <c>calendar-range:{entityName}:{calendarName}:{from:o}:{to:o}:{userPermsHash}:{cultureName}</c>.
/// </summary>
/// <remarks>
/// Calendar data is more volatile than the manifest (new events appear
/// continuously), so the matching FusionCache entry uses a shorter sliding
/// TTL — 1 minute by default (vs 5 min for the manifest). The user-perms-hash
/// gates per-RBAC partition so two callers with different role sets don't
/// share each other's cached results even when the window matches.
/// </remarks>
internal static class CalendarRangeCacheKey
{
    public const string Prefix = "calendar-range";

    /// <summary>
    /// Builds a deterministic cache key for one (entity, calendar, window, user, culture)
    /// tuple. The <c>from</c>/<c>to</c> values are formatted with the round-trip "o"
    /// specifier so two requests for the same window produce the same key regardless of
    /// the caller's culture.
    /// </summary>
    public static string Build(
        string entityName,
        string? calendarName,
        DateTimeOffset from,
        DateTimeOffset to,
        ClaimsPrincipal user,
        CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(culture);

        return $"{Prefix}:{entityName}:{calendarName ?? "*"}:{from:o}:{to:o}:{HashPermissions(user)}:{culture.Name}";
    }

    /// <summary>
    /// Cache eviction tag for every calendar range cached for one entity. Wolverine
    /// handlers reacting to entity-lifecycle events call
    /// <c>cache.RemoveByTagAsync(EvictionTag(entityName))</c> to drop every window
    /// in one go — finer-grained tagging would require knowing which window the
    /// changed row falls into, which the cache layer cannot determine without
    /// re-running the filter.
    /// </summary>
    public static string EvictionTag(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        return $"calendar:{entityName}";
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
