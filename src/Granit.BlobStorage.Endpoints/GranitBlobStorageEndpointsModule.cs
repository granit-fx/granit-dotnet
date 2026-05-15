using Granit.Authorization;
using Granit.BlobStorage.Endpoints.Workspaces;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.RateLimiting;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.BlobStorage.Endpoints;

/// <summary>
/// Granit module for blob storage administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBlobStorageModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitRateLimitingModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddFeatureProvider<BlobStorageFeatureProvider>();
    }
}
