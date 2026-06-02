using Granit.Hostnames.Endpoints.Endpoints;
using Granit.Hostnames.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Hostnames.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping managed hostname endpoints.
/// </summary>
public static class HostnamesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps managed hostname endpoints under <c>/{prefix}</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customise <see cref="HostnamesEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitHostnames(
        this IEndpointRouteBuilder endpoints,
        Action<HostnamesEndpointsOptions>? configure = null)
    {
        HostnamesEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        group.MapHostnamesReadEndpoints();
        group.MapHostnamesWriteEndpoints();

        return group;
    }
}
