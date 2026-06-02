using Granit.MultiTenancy.Endpoints.Endpoints;
using Granit.MultiTenancy.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.MultiTenancy.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping multi-tenancy management endpoints.
/// </summary>
public static class MultiTenancyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps multi-tenancy management endpoints under <c>/{prefix}/tenants</c>
    /// (default prefix: <c>multi-tenancy</c>, so the full path is <c>/multi-tenancy/tenants</c>).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="MultiTenancyEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitMultiTenancy(
        this IEndpointRouteBuilder endpoints,
        Action<MultiTenancyEndpointsOptions>? configure = null)
    {
        MultiTenancyEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Tenant listing is intentionally not mapped here — consumers register a Granit.QueryEngine
        // endpoint at the same prefix to keep Granit.MultiTenancy.Endpoints decoupled from EF Core
        // and the query engine. See the multi-tenancy module reference docs for the recipe.
        RouteGroupBuilder tenantsGroup = group.MapGranitGroup("tenants");
        tenantsGroup.MapMultiTenancyReadEndpoints();
        tenantsGroup.MapMultiTenancyWriteEndpoints();

        return group;
    }
}
