using Granit.Templating.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Templating.Endpoints.Workspaces;

/// <summary>Declares the templating module's features (per ADR-057).</summary>
internal sealed class TemplatingFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(TemplatingFeatures.Templates, f => f
            .Permission(TemplatingPermissions.Templates.Read)
            .RouteName(TemplatingFeatures.Templates)
            .DefaultIcon("layout-template")
            .DisplayKey("TemplatingEndpoints:Workspace.Templates"));

        catalog.Add(TemplatingFeatures.Categories, f => f
            .Permission(TemplatingPermissions.Categories.Read)
            .RouteName(TemplatingFeatures.Categories)
            .DefaultIcon("folder-tree")
            .DisplayKey("TemplatingEndpoints:Workspace.Categories"));
    }
}
