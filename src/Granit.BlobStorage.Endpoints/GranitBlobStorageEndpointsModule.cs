using Granit.Authorization;
using Granit.BlobStorage.Endpoints.Internal;
using Granit.BlobStorage.Endpoints.Workspaces;
using Granit.Http.RateLimiting;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;
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
    typeof(GranitHttpRateLimitingModule),
    typeof(GranitQueryEngineEndpointsModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<BlobStorageEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<BlobStorageFeatureProvider>();
    }
}
