using Granit.Http.SecurityHeaders.Endpoints.Endpoints;
using Granit.Http.SecurityHeaders.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Http.SecurityHeaders.Endpoints.Extensions;

/// <summary>
/// Extensions for mapping the CSP audit endpoint.
/// </summary>
public static class SecurityHeadersEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the CSP audit endpoint at <c>/{prefix}/csp</c>. Gated by
    /// <c>DiagnosticsPermissions.Monitoring.Read</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize the route prefix and OpenAPI tag.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitSecurityHeadersAudit(
        this IEndpointRouteBuilder endpoints,
        Action<SecurityHeadersEndpointsOptions>? configure = null)
    {
        SecurityHeadersEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        CspAuditEndpoints.Map(group);

        return group;
    }
}
