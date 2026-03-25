using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Endpoints;
using Granit.BlobStorage.Endpoints.Internal;
using Granit.BlobStorage.Endpoints.Options;
using Granit.QueryEngine.Endpoints.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
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
    /// app.MapBlobStorageEndpoints();
    ///
    /// // With custom options:
    /// app.MapBlobStorageEndpoints(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/blobs";
    ///     opts.RequiredRole = "storage-admin";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="BlobStorageEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapBlobStorageEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<BlobStorageEndpointsOptions>? configure = null)
    {
        BlobStorageEndpointsOptions options = new();
        configure?.Invoke(options);

        IOptions<AuthorizationOptions> authOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
        authOptions.Value.AddPolicy(
            BlobStorageAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(BlobStorageAuthorizationPolicy.PolicyName);

        group.MapReadEndpoints();
        group.MapWriteEndpoints();
        group.MapOperationEndpoints();

        // Use a temporary scope because IBlobQueryableProvider is Scoped
        // when EF Core persistence is registered and cannot be resolved from the root provider.
        bool hasQueryableProvider;
        using (IServiceScope scope = endpoints.ServiceProvider.CreateScope())
        {
            hasQueryableProvider = scope.ServiceProvider.GetService<IBlobQueryableProvider>() is not null;
        }

        if (hasQueryableProvider)
        {
            group.MapQueryEndpoints<BlobDescriptor>(
                "query",
                sp => sp.GetRequiredService<IBlobQueryableProvider>().GetDescriptors());
        }

        return group;
    }
}
