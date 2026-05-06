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

    // Claim types that affect authorization decisions and therefore the
    // shape of the cached calendar projection. Broadened beyond the original
    // (role, permission) allowlist (VULN-302): the permission claim type
    // varies across providers (perms, permissions, OpenIddict-prefixed forms),
    // and dropping any of them collapses two distinct callers onto the same
    // cache slot. Tenant claims are also included because the projection is
    // filtered by tenant downstream.
    private static readonly string[] AuthorizationClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles",
        "permission",
        "permissions",
        "perms",
        "scope",
        "scopes",
        "tenant",
        "tenant_id",
        "tid",
    ];

    private static string HashPermissions(ClaimsPrincipal user)
    {
        IEnumerable<string> claims = user.Claims
            .Where(c => Array.IndexOf(AuthorizationClaimTypes, c.Type) >= 0)
            .Select(c => $"{c.Type}={c.Value}")
            .OrderBy(s => s, StringComparer.Ordinal);

        StringBuilder buffer = new();
        foreach (string claim in claims)
        {
            buffer.Append(claim);
            buffer.Append('|');
        }

        string? sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        buffer.Append(sub ?? "anon");

        // Full SHA-256 hex (VULN-201) — the previous 64-bit truncation gave
        // a birthday-bound collision risk where two users in the same tenant
        // could share a cache slot, leaking each other's calendar projection.
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
