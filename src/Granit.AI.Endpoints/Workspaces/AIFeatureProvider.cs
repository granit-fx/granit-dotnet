using Granit.AI.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.AI.Endpoints.Workspaces;

/// <summary>Declares the AI module's features (per ADR-057).</summary>
internal sealed class AIFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(AIFeatures.Workspaces, f => f
            .Permission(AIPermissions.Workspaces.Read)
            .RouteName(AIFeatures.Workspaces)
            .DefaultIcon("sparkles")
            .DisplayKey("AIEndpoints:Workspace.Workspaces"));

        catalog.Add(AIFeatures.Usage, f => f
            .Permission(AIPermissions.Usage.Read)
            .RouteName(AIFeatures.Usage)
            .DefaultIcon("chart-bar")
            .DisplayKey("AIEndpoints:Workspace.Usage"));
    }
}
