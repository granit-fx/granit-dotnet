using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;
using Granit.Validation;

namespace Granit.BlobStorage.Endpoints;

/// <summary>
/// Granit module for blob storage administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBlobStorageModule),
    typeof(GranitQueryEngineEndpointsModule),
    typeof(GranitValidationModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule;
