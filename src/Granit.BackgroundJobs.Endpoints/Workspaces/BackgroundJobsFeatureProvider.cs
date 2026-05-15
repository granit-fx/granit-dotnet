using Granit.BackgroundJobs.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.BackgroundJobs.Endpoints.Workspaces;

/// <summary>Declares the background-jobs module's features (per ADR-057).</summary>
internal sealed class BackgroundJobsFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(BackgroundJobsFeatures.Jobs, f => f
            .Permission(BackgroundJobsPermissions.Jobs.Read)
            .RouteName(BackgroundJobsFeatures.Jobs)
            .DefaultIcon("clock")
            .DisplayKey("BackgroundJobsEndpoints:Workspace.Item"));
}
