using Granit.Authorization;
using Granit.Workspaces.Endpoints.Dtos;

namespace Granit.Workspaces.Endpoints.Internal;

/// <summary>Schema version for the workspace tree. Bump on breaking shape changes.</summary>
internal static class WorkspaceTreeSchema
{
    public const int Version = 1;
}

/// <summary>
/// Resolves the workspace tree for the requesting user, applying defense-in-depth
/// permission filtering at every level (workspace, section, item) and dropping
/// empty branches.
/// </summary>
internal sealed class WorkspaceFilter(IPermissionChecker permissionChecker)
{
    public async Task<WorkspaceTreeResponse> FilterAsync(
        IReadOnlyList<WorkspaceDescriptor> all,
        bool includeShells,
        CancellationToken cancellationToken)
    {
        // Collect every distinct permission referenced anywhere in the tree
        // and resolve them in a single batch — much cheaper than per-item
        // round-trips and gives us a closed view of what the caller can do
        // before we walk the descriptors.
        HashSet<string> referenced = new(StringComparer.Ordinal);
        foreach (WorkspaceDescriptor ws in all)
        {
            if (ws.RequiresPermission is { } wp)
            {
                referenced.Add(wp);
            }
            foreach (WorkspaceSectionDescriptor section in ws.Sections)
            {
                foreach (WorkspaceItemDescriptor item in section.Items)
                {
                    if (item.RequiresPermission is { } ip)
                    {
                        referenced.Add(ip);
                    }
                }
            }
        }

        HashSet<string> granted = referenced.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(
                await permissionChecker.GetGrantedAsync([.. referenced], cancellationToken).ConfigureAwait(false),
                StringComparer.Ordinal);

        List<WorkspaceResponse> workspaces = [];
        foreach (WorkspaceDescriptor ws in all)
        {
            if (ws.IsShell && !includeShells)
            {
                continue;
            }

            if (ws.RequiresPermission is { } wp && !granted.Contains(wp))
            {
                continue;
            }

            List<WorkspaceSectionResponse> sections = [];
            foreach (WorkspaceSectionDescriptor section in ws.Sections)
            {
                List<WorkspaceItemResponse> items = [];
                foreach (WorkspaceItemDescriptor item in section.Items)
                {
                    if (item.RequiresPermission is { } ip && !granted.Contains(ip))
                    {
                        continue;
                    }
                    items.Add(MapItem(item));
                }

                if (items.Count == 0)
                {
                    continue;
                }

                sections.Add(new WorkspaceSectionResponse(
                    section.Key,
                    section.DisplayKey,
                    section.Order,
                    section.CollapsedByDefault,
                    items));
            }

            // Empty workspace (shell with no surviving items, or a regular
            // workspace whose every section was filtered out) is dropped.
            if (sections.Count == 0)
            {
                continue;
            }

            workspaces.Add(new WorkspaceResponse(
                ws.Name,
                ws.DisplayKey,
                ws.Icon,
                ws.Order,
                ws.IsShell,
                sections));
        }

        return new WorkspaceTreeResponse(WorkspaceTreeSchema.Version, workspaces);
    }

    private static WorkspaceItemResponse MapItem(WorkspaceItemDescriptor item) =>
        new(
            item.Kind,
            item.Order,
            item.DisplayKey,
            item.Icon,
            item.EntityName,
            item.EntityViewName,
            item.EntityPresetOverlay,
            item.DashboardName,
            item.LinkUrl,
            item.SubWorkspaceName);
}
