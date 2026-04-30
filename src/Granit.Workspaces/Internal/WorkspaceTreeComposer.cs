using Microsoft.Extensions.Logging;

namespace Granit.Workspaces.Internal;

/// <summary>
/// Boot-time composer that merges every <see cref="WorkspaceDefinition"/>
/// with the contributions accumulated by all registered
/// <see cref="IWorkspaceContributor"/> implementations into a final
/// list of <see cref="WorkspaceDescriptor"/>s. Idempotent — produces the
/// same output for the same DI snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Merge rules (per ADR-040 §IoC):
/// </para>
/// <list type="bullet">
///   <item>Contributions targeting an unknown workspace name are dropped with a debug log
///         (silent skip — module load order is not deterministic).</item>
///   <item>When a contributor adds a section whose key matches an existing one on the
///         target workspace, the contributed items are appended; the existing
///         section's display key / order / collapse flag take precedence.</item>
///   <item>Items within a section are re-sorted by <c>Order</c> after merging.</item>
///   <item>Workspace tree depth is asserted ≤ 4 (ADR-040 §7) — a deeper graph throws.</item>
/// </list>
/// </remarks>
internal static class WorkspaceTreeComposer
{
    /// <summary>Maximum allowed depth of the workspace tree (ADR-040 §7).</summary>
    public const int MaxDepth = 4;

    public static IReadOnlyList<WorkspaceDescriptor> Compose(
        IEnumerable<IWorkspaceDescriptor> definitions,
        IEnumerable<IWorkspaceContributor> contributors,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(logger);

        Dictionary<string, WorkspaceDescriptor> byName = new(StringComparer.Ordinal);
        foreach (IWorkspaceDescriptor def in definitions)
        {
            if (!byName.TryAdd(def.Name, def.Descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate WorkspaceDefinition name '{def.Name}'. Names must be unique across all loaded modules.");
            }
        }

        WorkspaceContributionContext ctx = new();
        foreach (IWorkspaceContributor contributor in contributors)
        {
            contributor.Contribute(ctx);
        }

        foreach ((string targetName, List<WorkspaceSectionDescriptor> contributedSections) in ctx.Contributions)
        {
            if (!byName.TryGetValue(targetName, out WorkspaceDescriptor? target))
            {
                logger.LogDebug(
                    "Workspace contribution targeted unknown workspace '{TargetName}' — dropped.",
                    targetName);
                continue;
            }

            byName[targetName] = MergeContributions(target, contributedSections);
        }

        IReadOnlyList<WorkspaceDescriptor> all = [.. byName.Values
            .Where(static w => !IsEmptyShell(w))
            .OrderBy(static w => w.Order)
            .ThenBy(static w => w.Name, StringComparer.Ordinal)];

        AssertDepthInvariant(all);

        return all;
    }

    /// <summary>Whether this workspace is a shell with no items after merging — auto-filtered from the rendered tree.</summary>
    private static bool IsEmptyShell(WorkspaceDescriptor ws) =>
        ws.IsShell && ws.Sections.All(static s => s.Items.Count == 0);

    private static WorkspaceDescriptor MergeContributions(
        WorkspaceDescriptor target,
        List<WorkspaceSectionDescriptor> contributedSections)
    {
        var sectionsByKey = target.Sections
            .ToDictionary(s => s.Key, StringComparer.Ordinal);

        foreach (WorkspaceSectionDescriptor contributed in contributedSections)
        {
            if (sectionsByKey.TryGetValue(contributed.Key, out WorkspaceSectionDescriptor? existing))
            {
                List<WorkspaceItemDescriptor> mergedItems =
                    [.. existing.Items, .. contributed.Items];
                mergedItems.Sort(static (a, b) => a.Order.CompareTo(b.Order));

                sectionsByKey[contributed.Key] = existing with { Items = mergedItems };
            }
            else
            {
                sectionsByKey[contributed.Key] = contributed;
            }
        }

        IReadOnlyList<WorkspaceSectionDescriptor> mergedSections = [.. sectionsByKey.Values
            .OrderBy(static s => s.Order)
            .ThenBy(static s => s.Key, StringComparer.Ordinal)];

        return target with { Sections = mergedSections };
    }

    private static void AssertDepthInvariant(IReadOnlyList<WorkspaceDescriptor> all)
    {
        var byName = all.ToDictionary(w => w.Name, StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);

        foreach (WorkspaceDescriptor root in all)
        {
            int depth = ComputeDepth(root, byName, visiting, currentDepth: 1);
            if (depth > MaxDepth)
            {
                throw new InvalidOperationException(
                    $"Workspace tree exceeds maximum depth ({MaxDepth}). " +
                    $"Workspace '{root.Name}' resolves to depth {depth}. See ADR-040 §7.");
            }
        }
    }

    private static int ComputeDepth(
        WorkspaceDescriptor current,
        IReadOnlyDictionary<string, WorkspaceDescriptor> byName,
        HashSet<string> visiting,
        int currentDepth)
    {
        if (!visiting.Add(current.Name))
        {
            // Cycle — break with a synthetic depth bump to fail the invariant.
            return MaxDepth + 1;
        }

        int maxChildDepth = currentDepth;
        try
        {
            foreach (WorkspaceSectionDescriptor section in current.Sections)
            {
                foreach (WorkspaceItemDescriptor item in section.Items)
                {
                    if (item.Kind != WorkspaceItemKind.SubWorkspace
                        || item.SubWorkspaceName is not { } childName
                        || !byName.TryGetValue(childName, out WorkspaceDescriptor? child))
                    {
                        continue;
                    }

                    int childDepth = ComputeDepth(child, byName, visiting, currentDepth + 1);
                    if (childDepth > maxChildDepth)
                    {
                        maxChildDepth = childDepth;
                    }
                }
            }
        }
        finally
        {
            visiting.Remove(current.Name);
        }

        return maxChildDepth;
    }
}
