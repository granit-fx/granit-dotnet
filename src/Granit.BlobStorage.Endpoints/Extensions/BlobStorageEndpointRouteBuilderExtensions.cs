using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Endpoints;
using Granit.BlobStorage.Endpoints.Options;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering blob storage administration endpoints.
/// </summary>
public static class BlobStorageEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the blob storage administration endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitBlobStorage();
    ///
    /// // With custom options:
    /// app.MapGranitBlobStorage(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/blobs";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="BlobStorageEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitBlobStorage(
        this IEndpointRouteBuilder endpoints,
        Action<BlobStorageEndpointsOptions>? configure = null)
    {
        BlobStorageEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptions<BlobStorageEndpointsOptions>>()?.Value
            ?? new BlobStorageEndpointsOptions();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Each permission tier gets its own "blobs" group: authorization added to a RouteGroupBuilder
        // is a convention that applies to EVERY endpoint mapped from it, so reusing a single group would
        // make every endpoint require the union of all policies — read endpoints would silently demand
        // Manage as well. Distinct group builders sharing the same URL prefix keep the conventions isolated.
        group.MapGranitGroup("blobs")
            .RequireAuthorization(BlobStoragePermissions.Administration.Read)
            .MapReadEndpoints();

        group.MapGranitGroup("blobs")
            .RequireAuthorization(BlobStoragePermissions.Administration.Manage)
            .MapWriteEndpoints();

        group.MapGranitGroup("blobs")
            .RequireAuthorization(BlobStoragePermissions.Administration.Manage)
            .MapOperationEndpoints();

        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. To expose cross-tenant blob-descriptor visibility, mark this
        // route .AllowHostAccess(); a platform admin holding Administration.Read at global scope then
        // reads across tenants, while the multi-tenant filter stays enforced for tenant callers.
        group.MapGranitGroup("blobs")
            .RequireAuthorization(BlobStoragePermissions.Administration.Read)
            .MapGranitQuery<BlobDescriptor>();

        return group;
    }
}
