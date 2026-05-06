using System.Collections.Frozen;
using System.Security.Claims;
using Granit.Activities.Domain;

namespace Granit.Activities.Endpoints.Authorization;

/// <summary>
/// Composes every registered <see cref="IActivityHostAuthorizationProvider"/>
/// into a single gate used by the endpoints. Lookups index by
/// <see cref="IActivityHostAuthorizationProvider.EntityType"/> with the
/// <c>"*"</c> wildcard as the fallback.
/// </summary>
internal sealed class ActivityHostAuthorizer
{
    private readonly FrozenDictionary<string, IActivityHostAuthorizationProvider> _providers;
    private readonly IActivityHostAuthorizationProvider _wildcard;

    public ActivityHostAuthorizer(IEnumerable<IActivityHostAuthorizationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        Dictionary<string, IActivityHostAuthorizationProvider> map = new(StringComparer.Ordinal);
        IActivityHostAuthorizationProvider? wildcard = null;
        foreach (IActivityHostAuthorizationProvider p in providers)
        {
            if (p.EntityType == "*")
            {
                wildcard = p;
                continue;
            }
            // Last-write-wins on duplicate keys — host modules opt out of the
            // wildcard by registering a stricter provider for the same EntityType.
            map[p.EntityType] = p;
        }
        _providers = map.ToFrozenDictionary(StringComparer.Ordinal);
        _wildcard = wildcard ?? new AllowAllActivityHostAuthorizationProvider();
    }

    /// <summary>
    /// Returns <see langword="true"/> iff the matching provider (or the wildcard
    /// fallback) authorizes the caller to see activities pinned to
    /// <paramref name="entityType"/> / <paramref name="entityId"/>.
    /// </summary>
    public ValueTask<bool> CanReadHostAsync(string entityType, Guid entityId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        IActivityHostAuthorizationProvider provider = _providers.TryGetValue(entityType, out IActivityHostAuthorizationProvider? hit)
            ? hit
            : _wildcard;
        return provider.CanReadHostAsync(entityId, user, cancellationToken);
    }

    /// <summary>
    /// Filters <paramref name="rows"/> in-memory, dropping activities whose
    /// host the caller cannot read. Used by list / calendar projections.
    /// </summary>
    public async ValueTask<IReadOnlyList<Activity>> FilterAsync(
        IReadOnlyList<Activity> rows,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        // Cache results per (entityType, entityId) — list/calendar pages often
        // contain multiple activities pinned to the same host.
        Dictionary<(string, Guid), bool> cache = new();
        List<Activity> filtered = new(rows.Count);
        foreach (Activity row in rows)
        {
            (string, Guid) key = (row.EntityType, row.EntityId);
            if (!cache.TryGetValue(key, out bool allowed))
            {
                allowed = await CanReadHostAsync(row.EntityType, row.EntityId, user, cancellationToken).ConfigureAwait(false);
                cache[key] = allowed;
            }
            if (allowed)
            {
                filtered.Add(row);
            }
        }
        return filtered;
    }
}
