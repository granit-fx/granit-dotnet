using Granit.Analytics.Endpoints.Endpoints;
using Granit.Analytics.Endpoints.Options;
using Granit.Analytics.Endpoints.Permissions;
using Granit.Authorization.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <see cref="AnalyticsEndpointsOptions"/> is resolved from DI — it is bound from the
    /// <c>AnalyticsEndpoints</c> section of <c>appsettings.json</c> by
    /// <c>AddGranitAnalyticsEndpoints</c>. Override per-app via configuration.
    /// </remarks>
    public static RouteGroupBuilder MapGranitAnalytics(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        AnalyticsEndpointsOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<AnalyticsEndpointsOptions>>().Value;

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.MetricsTagName);

        group.RequireAuthorization(AnalyticsPermissions.Metrics.Read).MapMetricEndpoints();
        // Per-widget render endpoints (P3 / option B) — single widget rendered
        // ad-hoc from a typed WidgetDefinition body, returning the same
        // DashboardRenderedWidgetResponse shape the bundle path emits. Gated
        // by the same Metrics.Read permission since the snapshot semantics
        // mirror the bundle path's per-widget envelope.
        group.RequireAuthorization(AnalyticsPermissions.Metrics.Read).MapWidgetRenderEndpoints();

        return group;
    }
}
