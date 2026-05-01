using Granit.Entities.Actions;
using Microsoft.Extensions.Logging;

namespace Granit.Entities.Internal;

/// <summary>
/// Boot-time merger that runs every registered <see cref="IEntityActionContributor"/>
/// and folds the contributed actions into the matching
/// <see cref="EntityDefinitionDescriptor"/>. Mirrors
/// <see cref="EntityRelationMerger"/>; same merge rules:
/// <list type="bullet">
///   <item>Contributions targeting an unknown source entity are dropped with a debug log.</item>
///   <item>When a contributor declares an action whose name matches an existing intra-module
///         action on the source, the intra-module declaration takes precedence — the
///         contribution is dropped with a debug log.</item>
///   <item>Final action list is sorted by <c>Order</c> then <c>Name</c>.</item>
/// </list>
/// </summary>
internal static class EntityActionMerger
{
    public static IReadOnlyList<IEntityDefinitionDescriptor> Merge(
        IEnumerable<IEntityDefinitionDescriptor> definitions,
        IEnumerable<IEntityActionContributor> contributors,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(logger);

        IEntityDefinitionDescriptor[] frozen = [.. definitions];

        EntityActionContributionContext context = new();
        foreach (IEntityActionContributor contributor in contributors)
        {
            contributor.Contribute(context);
        }

        if (context.Contributions.Count == 0)
        {
            return frozen;
        }

        Dictionary<Type, IEntityDefinitionDescriptor> byType = frozen.ToDictionary(d => d.EntityType);

        foreach ((Type sourceType, IReadOnlyList<EntityActionDescriptor> contributed) in context.Contributions)
        {
            if (!byType.TryGetValue(sourceType, out IEntityDefinitionDescriptor? hostDescriptor))
            {
                logger.LogDebug(
                    "Entity action contribution targeted unknown source CLR type '{SourceType}' — dropped.",
                    sourceType.FullName);
                continue;
            }

            EntityDefinitionDescriptor merged = MergeActions(hostDescriptor.Descriptor, contributed, logger);
            byType[sourceType] = new MergedActionsEntityDefinitionDescriptor(hostDescriptor, merged);
        }

        return [.. byType.Values];
    }

    private static EntityDefinitionDescriptor MergeActions(
        EntityDefinitionDescriptor host,
        IReadOnlyList<EntityActionDescriptor> contributed,
        ILogger logger)
    {
        var intraModuleNames = host.Actions
            .Where(a => a.ContributorAssemblyName is null)
            .Select(a => a.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<EntityActionDescriptor> merged = [.. host.Actions];

        foreach (EntityActionDescriptor c in contributed)
        {
            if (intraModuleNames.Contains(c.Name))
            {
                logger.LogDebug(
                    "Cross-module action contribution '{Name}' on entity '{Entity}' was dropped because the source entity already declares an action with the same name.",
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

        return host with { Actions = merged };
    }

    private sealed class MergedActionsEntityDefinitionDescriptor(
        IEntityDefinitionDescriptor inner,
        EntityDefinitionDescriptor merged) : IEntityDefinitionDescriptor
    {
        public string Name => inner.Name;
        public Type EntityType => inner.EntityType;
        public EntityDefinitionDescriptor Descriptor { get; } = merged;
    }
}
