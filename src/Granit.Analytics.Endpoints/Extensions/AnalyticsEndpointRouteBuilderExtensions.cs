using Granit.Analytics.Endpoints.Endpoints;
using Granit.Analytics.Endpoints.Options;
using Granit.Analytics.Endpoints.Permissions;
using Granit.Authorization.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Analytics.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering Granit.Analytics HTTP endpoints.
/// </summary>
public static class AnalyticsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Granit.Analytics metric endpoints onto <paramref name="endpoints"/>.
    /// Final route: <c>POST {prefix}/metrics/{name}</c> guarded by
    /// <see cref="AnalyticsPermissions.Metrics.Read"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AnalyticsEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitAnalytics(
        this IEndpointRouteBuilder endpoints,
        Action<AnalyticsEndpointsOptions>? configure = null)
    {
        AnalyticsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.MetricsTagName);

        group.RequireAuthorization(AnalyticsPermissions.Metrics.Read).MapMetricEndpoints();

        return group;
    }
}
