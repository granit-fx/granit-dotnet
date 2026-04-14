using Granit.Authorization;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Queries;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.QueryEngine.Extensions;
using Granit.RateLimiting;
using Granit.Validation;

namespace Granit.BlobStorage.Endpoints;

/// <summary>
/// Granit module for blob storage administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBlobStorageModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitRateLimitingModule),
    typeof(GranitValidationModule))]
public sealed class GranitBlobStorageEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddQueryDefinition<BlobDescriptor, BlobDescriptorQueryDefinition>();
}
