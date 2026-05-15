using Granit.Scheduling.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Scheduling.Endpoints.Workspaces;

/// <summary>Declares the scheduling module's features (per ADR-057).</summary>
internal sealed class SchedulingFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(SchedulingFeatures.Actions, f => f
            .Permission(SchedulingPermissions.Actions.Read)
            .RouteName(SchedulingFeatures.Actions)
            .DefaultIcon("calendar-clock")
            .DisplayKey("SchedulingEndpoints:Workspace.Item"));
}
