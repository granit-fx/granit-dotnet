using Granit.AuditLog.Endpoints.Endpoints;
using Granit.AuditLog.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.AuditLog.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering audit log API endpoints.
/// </summary>
public static class AuditLogEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps read-only audit log endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditLogEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>All endpoints are protected by the configured authorization policy.</para>
    /// <para>Call from your application:</para>
    /// <code>
    /// app.MapAuditLogEndpoints();
    /// app.MapAuditLogEndpoints(opts =&gt; opts.AuthorizationPolicy = "Custom.Policy");
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapAuditLogEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<AuditLogEndpointsOptions>? configure = null)
    {
        AuditLogEndpointsOptions options = new();
        configure?.Invoke(options);

        // Register the authorization policy (role-based fallback).
        IOptions<AuthorizationOptions>? authOptions =
            endpoints.ServiceProvider.GetService<IOptions<AuthorizationOptions>>();
        authOptions?.Value.AddPolicy(
            options.AuthorizationPolicy,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(options.AuthorizationPolicy);

        group.MapAuditLogReadEndpoints();

        return group;
    }
}
