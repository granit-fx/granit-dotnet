using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Querying.Endpoints;

namespace Granit.BlobStorage.Endpoints;

/// <summary>
/// Granit module for blob storage administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBlobStorageModule),
    typeof(GranitQueryingEndpointsModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule;
