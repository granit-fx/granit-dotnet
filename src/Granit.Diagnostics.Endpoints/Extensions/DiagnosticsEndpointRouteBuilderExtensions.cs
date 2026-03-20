using Granit.Diagnostics.Endpoints.Endpoints;
using Granit.Diagnostics.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Diagnostics.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping diagnostics monitoring endpoints.
/// </summary>
public static class DiagnosticsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps diagnostics monitoring endpoints under <c>/{prefix}/diagnostics</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="DiagnosticsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitDiagnosticsMonitoring(
        this IEndpointRouteBuilder endpoints,
        Action<DiagnosticsEndpointsOptions>? configure = null)
    {
        DiagnosticsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        MonitoringEndpoints.Map(group);

        return group;
    }
}
