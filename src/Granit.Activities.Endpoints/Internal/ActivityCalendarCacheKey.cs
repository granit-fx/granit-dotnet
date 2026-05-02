using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Activities.Endpoints.Internal;

/// <summary>
/// Cache-key composer for the activities calendar endpoint (story #1801).
/// Format: <c>activity-calendar:{tenantOrGlobal}:{userPermsHash}:{from-iso}:{to-iso}:{assignee?}:{entityType?}:{type?}:{status?}</c>.
/// </summary>
internal static class ActivityCalendarCacheKey
{
    public const string Prefix = "activity-calendar";

    public static string Build(
        string? tenantId,
        ClaimsPrincipal user,
        DateTimeOffset from,
        DateTimeOffset to,
        string? assigneeFilter,
        string? entityTypeFilter,
        string? typeFilter,
        string? statusFilter)
    {
        ArgumentNullException.ThrowIfNull(user);

        StringBuilder sb = new();
        sb.Append(Prefix).Append(':')
          .Append(tenantId ?? "global").Append(':')
          .Append(HashPermissions(user)).Append(':')
          .Append(from.ToString("O", CultureInfo.InvariantCulture)).Append(':')
          .Append(to.ToString("O", CultureInfo.InvariantCulture)).Append(':')
          .Append(assigneeFilter ?? string.Empty).Append(':')
          .Append(entityTypeFilter ?? string.Empty).Append(':')
          .Append(typeFilter ?? string.Empty).Append(':')
          .Append(statusFilter ?? string.Empty);
        return sb.ToString();
    }

    /// <summary>
    /// Per-tenant eviction tag — drops every cached calendar window for the
    /// given tenant in one <c>RemoveByTagAsync</c> call when an activity is
    /// created, updated, or deleted in that tenant.
    /// </summary>
    public static string EvictionTag(Guid? tenantId) =>
        $"{Prefix}:tenant:{tenantId?.ToString() ?? "global"}";

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
