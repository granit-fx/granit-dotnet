using Granit.Workflow.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Workflow.Endpoints.Workspaces;

/// <summary>Declares the workflow module's features (per ADR-057).</summary>
internal sealed class WorkflowFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(WorkflowFeatures.History, f => f
            .Permission(WorkflowPermissions.History.Read)
            .RouteName(WorkflowFeatures.History)
            .DefaultIcon("git-branch")
            .DisplayKey("WorkflowEndpoints:Workspace.History"));
}
