using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Cache-key composer for the relation aggregates endpoint (story #1562).
/// Format: <c>relation-agg:{sourceEntity}:{sourceId}:{relationName}:{userPermsHash}:{cultureName}</c>.
/// </summary>
/// <remarks>
/// Per-relation keys (not per-batch) so a detail page that requests 5
/// relations only re-computes the ones whose entries have aged past the
/// 30-second sliding TTL — instead of invalidating the whole batch when
/// any single counter changes. The user-perms-hash gates per-RBAC partition
/// so two callers with different roles don't share each other's cached
/// results even when the source row matches.
/// </remarks>
internal static class RelationAggregateCacheKey
{
    public const string Prefix = "relation-agg";

    public static string Build(
        string sourceEntityName,
        string sourceId,
        string relationName,
        ClaimsPrincipal user,
        CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relationName);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(culture);

        return $"{Prefix}:{sourceEntityName}:{sourceId}:{relationName}:{HashPermissions(user)}:{culture.Name}";
    }

    /// <summary>
    /// Cache eviction tag for all aggregates of a given (sourceEntity, sourceId).
    /// Used when the source row itself changes — drops every cached aggregate for
    /// that one row across every relation.
    /// </summary>
    public static string EvictionTag(string sourceEntityName, string sourceId) =>
        $"entity:{sourceEntityName}:{sourceId}";

    /// <summary>
    /// Cache eviction tag for all aggregates of a given relation, across every
    /// source row (story #1793). Used by the EF Core relation-aggregate
    /// invalidator: when any row of the related entity changes, the per-relation
    /// tag drops every cached counter for that relation in one call. Coarser than
    /// the per-(source, id) tag but the only viable scheme without an executable
    /// foreign-key predicate on <see cref="RelationDescriptor"/>.
    /// </summary>
    public static string EvictionTagForRelation(string sourceEntityName, string relationName) =>
        $"entity-relation:{sourceEntityName}:{relationName}";

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
