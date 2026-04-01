using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Endpoints;
using Granit.BlobStorage.Endpoints.Options;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.QueryEngine.Endpoints.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
        BlobStorageEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.RequireAuthorization(BlobStoragePermissions.Administration.Read).MapReadEndpoints();
        group.RequireAuthorization(BlobStoragePermissions.Administration.Manage).MapWriteEndpoints();
        group.RequireAuthorization(BlobStoragePermissions.Administration.Manage).MapOperationEndpoints();

        // Use a temporary scope because IBlobQueryableProvider is Scoped
        // when EF Core persistence is registered and cannot be resolved from the root provider.
        bool hasQueryableProvider;
        using (IServiceScope scope = endpoints.ServiceProvider.CreateScope())
        {
            hasQueryableProvider = scope.ServiceProvider.GetService<IBlobQueryableProvider>() is not null;
        }

        if (hasQueryableProvider)
        {
            group.RequireAuthorization(BlobStoragePermissions.Administration.Read).MapGranitQuery<BlobDescriptor>(
                "query",
                sp => sp.GetRequiredService<IBlobQueryableProvider>().GetDescriptors());
        }

        return group;
    }
}
