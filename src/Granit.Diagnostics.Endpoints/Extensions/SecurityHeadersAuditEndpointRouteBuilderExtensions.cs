using Granit.Diagnostics.Endpoints.Endpoints;
using Granit.Diagnostics.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Diagnostics.Endpoints.Extensions;

/// <summary>
/// Extensions for mapping the CSP audit endpoint.
/// </summary>
public static class SecurityHeadersAuditEndpointRouteBuilderExtensions
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
        Action<SecurityHeadersAuditOptions>? configure = null)
    {
        // Bound from Diagnostics:Endpoints:SecurityHeadersAudit by the module, with
        // the delegate overriding on top.
        SecurityHeadersAuditOptions options =
            endpoints.ServiceProvider.GetService<IOptions<SecurityHeadersAuditOptions>>()?.Value ?? new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        CspAuditEndpoints.Map(group);

        return group;
    }
}
