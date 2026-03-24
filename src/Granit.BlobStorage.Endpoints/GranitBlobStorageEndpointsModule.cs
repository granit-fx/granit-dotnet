using Granit.Authorization;
using Granit.Modularity;
using Granit.Querying.Endpoints;
using Granit.Validation;

namespace Granit.BlobStorage.Endpoints;

/// <summary>
/// Granit module for blob storage administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBlobStorageModule),
    typeof(GranitQueryingEndpointsModule),
    typeof(GranitValidationModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule;
