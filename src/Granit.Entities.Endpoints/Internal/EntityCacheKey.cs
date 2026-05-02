using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Builds FusionCache keys for the manifest + discovery endpoints. The key
/// embeds the user's permission set so two callers with different RBAC
/// snapshots never see each other's filtered payloads — even when their tenant
/// and culture match.
/// </summary>
internal static class EntityCacheKey
{
    /// <summary>Cache-key prefix for the per-entity manifest endpoint.</summary>
    public const string ManifestPrefix = "entity-meta";

    /// <summary>Cache-key prefix for the discovery tree endpoint.</summary>
    public const string DiscoveryPrefix = "entity-discovery";

    /// <summary>Cache-key prefix for the calendar range-query endpoint.</summary>
    public const string CalendarRangePrefix = "entity-calendar";

    /// <summary>
    /// Builds <c>entity-meta:{entityName}:{userPermsHash}:{cultureName}</c>.
    /// </summary>
    public static string ForManifest(string entityName, ClaimsPrincipal user, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(culture);

        return $"{ManifestPrefix}:{entityName}:{HashPermissions(user)}:{culture.Name}";
    }

    /// <summary>
    /// Builds <c>entity-discovery:{userPermsHash}:{cultureName}</c>.
    /// </summary>
    public static string ForDiscovery(ClaimsPrincipal user, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(culture);

        return $"{DiscoveryPrefix}:{HashPermissions(user)}:{culture.Name}";
    }

    /// <summary>
    /// Builds <c>entity-calendar:{entityName}:{calendarName}:{userPermsHash}:{fromTicks}-{toTicks}</c>.
    /// The calendar name slot is <c>_default</c> when the request did not pin a
    /// specific layout (today the framework rejects duplicate calendar layouts,
    /// so the explicit name is reserved for the day multi-instance layouts ship
    /// per ADR-042 §4 — same trade-off as the manifest cache key).
    /// </summary>
    public static string ForCalendarRange(
        string entityName,
        string? calendarName,
        ClaimsPrincipal user,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(user);

        string calendarSlot = string.IsNullOrWhiteSpace(calendarName) ? "_default" : calendarName;
        long fromTicks = from.UtcTicks;
        long toTicks = to.UtcTicks;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{CalendarRangePrefix}:{entityName}:{calendarSlot}:{HashPermissions(user)}:{fromTicks}-{toTicks}");
    }

    /// <summary>
    /// SHA-256 over the sorted role + permission claim values. 16 hex chars is
    /// enough — collisions on this surface only mean a stale entry is served
    /// up to the next TTL boundary; security gating runs separately on every
    /// request.
    /// </summary>
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
