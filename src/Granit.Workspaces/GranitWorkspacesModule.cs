using Granit.Modularity;
using Granit.Workspaces.Extensions;

namespace Granit.Workspaces;

/// <summary>
/// Granit module for the workspace-tree runtime (per ADR-040). Hosts the
/// <see cref="IWorkspaceRegistry"/> built at boot from every registered
/// <see cref="WorkspaceDefinition"/> merged with <see cref="IWorkspaceContributor"/>
/// grafts.
/// </summary>
[DependsOn(typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitWorkspacesModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitWorkspaces();
}
