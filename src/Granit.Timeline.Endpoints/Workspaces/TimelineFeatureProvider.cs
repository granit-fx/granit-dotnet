using Granit.Timeline.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Timeline.Endpoints.Workspaces;

/// <summary>Declares the timeline module's features (per ADR-057).</summary>
internal sealed class TimelineFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(TimelineFeatures.Entries, f => f
            .Permission(TimelinePermissions.Entries.Read)
            .RouteName(TimelineFeatures.Entries)
            .DefaultIcon("activity")
            .DisplayKey("TimelineEndpoints:Workspace.Item"));
}
