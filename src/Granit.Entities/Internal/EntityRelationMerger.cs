using Granit.Entities.Relations;
using Microsoft.Extensions.Logging;

namespace Granit.Entities.Internal;

/// <summary>
/// Boot-time merger that runs every registered
/// <see cref="IEntityRelationContributor"/> and folds the contributed relations
/// into the matching <see cref="EntityDefinitionDescriptor"/>. Idempotent —
/// produces the same output for the same DI snapshot.
/// </summary>
/// <remarks>
/// Merge rules (mirror the workspace composer in <c>Granit.Workspaces</c>):
/// <list type="bullet">
///   <item>Contributions targeting an unknown source entity are dropped with a
///         debug log (silent skip).</item>
///   <item>When a contributor declares a relation whose name matches an existing
///         intra-module relation on the source, the intra-module declaration
///         takes precedence — the contribution is dropped with a debug log.</item>
///   <item>Final relation list is sorted by <c>Order</c> then <c>Name</c>.</item>
/// </list>
/// </remarks>
internal static class EntityRelationMerger
{
    public static IReadOnlyList<IEntityDefinitionDescriptor> Merge(
        IEnumerable<IEntityDefinitionDescriptor> definitions,
        IEnumerable<IEntityRelationContributor> contributors,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(logger);

        IEntityDefinitionDescriptor[] frozen = [.. definitions];

        EntityRelationContributionContext context = new();
        foreach (IEntityRelationContributor contributor in contributors)
        {
            contributor.Contribute(context);
        }

        if (context.Contributions.Count == 0)
        {
            return frozen;
        }

        Dictionary<Type, IEntityDefinitionDescriptor> byType = frozen.ToDictionary(d => d.EntityType);

        foreach ((Type sourceType, IReadOnlyList<RelationDescriptor> contributed) in context.Contributions)
        {
            if (!byType.TryGetValue(sourceType, out IEntityDefinitionDescriptor? hostDescriptor))
            {
                logger.LogDebug(
                    "Entity relation contribution targeted unknown source CLR type '{SourceType}' — dropped.",
                    sourceType.FullName);
                continue;
            }

            EntityDefinitionDescriptor merged = MergeRelations(hostDescriptor.Descriptor, contributed, logger);
            byType[sourceType] = new MergedEntityDefinitionDescriptor(hostDescriptor, merged);
        }

        return [.. byType.Values];
    }

    private static EntityDefinitionDescriptor MergeRelations(
        EntityDefinitionDescriptor host,
        IReadOnlyList<RelationDescriptor> contributed,
        ILogger logger)
    {
        var intraModuleNames = host.Relations
            .Where(r => r.ContributorAssemblyName is null)
            .Select(r => r.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<RelationDescriptor> merged = [.. host.Relations];

        foreach (RelationDescriptor c in contributed)
        {
            if (intraModuleNames.Contains(c.Name))
            {
                logger.LogDebug(
                    "Cross-module relation contribution '{Name}' on entity '{Entity}' was dropped because the source entity already declares a relation with the same name.",
                    c.Name, host.Name);
                continue;
            }
            merged.Add(c);
        }

        merged.Sort(static (a, b) =>
        {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        return host with { Relations = merged };
    }

    /// <summary>
    /// Adapter that decorates the original <see cref="IEntityDefinitionDescriptor"/>
    /// reference with the merged descriptor, so consumers reading
    /// <see cref="IEntityDefinitionDescriptor.Descriptor"/> see the post-merge
    /// relations without losing the original singleton's identity.
    /// </summary>
    private sealed class MergedEntityDefinitionDescriptor(
        IEntityDefinitionDescriptor inner,
        EntityDefinitionDescriptor merged) : IEntityDefinitionDescriptor
    {
        public string Name => inner.Name;
        public Type EntityType => inner.EntityType;
        public EntityDefinitionDescriptor Descriptor { get; } = merged;
    }
}
