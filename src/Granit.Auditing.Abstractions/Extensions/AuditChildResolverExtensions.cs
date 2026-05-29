namespace Granit.Auditing.Extensions;

/// <summary>
/// Helpers for unioning the contributions of multiple
/// <see cref="IAuditChildResolver"/> instances and shaping the result for the
/// batch read path.
/// </summary>
public static class AuditChildResolverExtensions
{
    /// <summary>
    /// Invokes every resolver and merges their scopes by
    /// <see cref="AuditChildScope.ChildEntityType"/>, deduplicating child ids
    /// with ordinal comparison. Returns an empty collection when no resolver
    /// is registered — callers append the parent ref themselves.
    /// </summary>
    public static async Task<IReadOnlyCollection<AuditChildScope>> ResolveAllAsync(
        this IEnumerable<IAuditChildResolver> resolvers,
        string parentEntityType,
        string parentEntityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        ArgumentException.ThrowIfNullOrEmpty(parentEntityType);
        ArgumentException.ThrowIfNullOrEmpty(parentEntityId);

        Dictionary<string, HashSet<string>>? merged = null;

        foreach (IAuditChildResolver resolver in resolvers)
        {
            IReadOnlyCollection<AuditChildScope> scopes = await resolver
                .ResolveAsync(parentEntityType, parentEntityId, cancellationToken)
                .ConfigureAwait(false);

            foreach (AuditChildScope scope in scopes)
            {
                if (scope.ChildEntityIds.Count == 0)
                {
                    continue;
                }

                merged ??= new(StringComparer.Ordinal);
                if (!merged.TryGetValue(scope.ChildEntityType, out HashSet<string>? ids))
                {
                    ids = new(StringComparer.Ordinal);
                    merged[scope.ChildEntityType] = ids;
                }

                foreach (string id in scope.ChildEntityIds)
                {
                    ids.Add(id);
                }
            }
        }

        if (merged is null)
        {
            return [];
        }

        var result = new AuditChildScope[merged.Count];
        int i = 0;
        foreach ((string type, HashSet<string> ids) in merged)
        {
            result[i++] = new AuditChildScope(type, ids);
        }
        return result;
    }

    /// <summary>
    /// Flattens a parent ref and a collection of child scopes into the
    /// <see cref="AuditEntityRef"/> set consumed by
    /// <see cref="IAuditingReader.GetByEntitiesAsync"/>. The parent ref is
    /// always included first; child refs follow grouped by type.
    /// </summary>
    public static IReadOnlyCollection<AuditEntityRef> ToTargets(
        this IReadOnlyCollection<AuditChildScope> scopes,
        AuditEntityRef parent)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        int total = 1;
        foreach (AuditChildScope scope in scopes)
        {
            total += scope.ChildEntityIds.Count;
        }

        List<AuditEntityRef> targets = new(total) { parent };
        foreach (AuditChildScope scope in scopes)
        {
            foreach (string id in scope.ChildEntityIds)
            {
                targets.Add(new AuditEntityRef(scope.ChildEntityType, id));
            }
        }
        return targets;
    }
}
