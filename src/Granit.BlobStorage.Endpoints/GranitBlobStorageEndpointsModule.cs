using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;
using Granit.RateLimiting;
using Granit.Validation;

namespace Granit.BlobStorage.Endpoints;

/// <summary>
/// Granit module for blob storage administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBlobStorageModule),
    typeof(GranitQueryEngineEndpointsModule),
    typeof(GranitRateLimitingModule),
    typeof(GranitValidationModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule;
