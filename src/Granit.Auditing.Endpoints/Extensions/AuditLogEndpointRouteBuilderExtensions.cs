using Granit.Auditing.Endpoints.Endpoints;
using Granit.Auditing.Endpoints.Options;
using Granit.Auditing.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Auditing.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering audit log API endpoints.
/// </summary>
public static class AuditLogEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps read-only audit log endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditingEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>All endpoints are protected by the <c>AuditLog.Entries.Read</c> permission.</para>
    /// <para>Call from your application:</para>
    /// <code>
    /// app.MapAuditingEndpoints();
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapAuditingEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<AuditingEndpointsOptions>? configure = null)
    {
        AuditingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(AuditLogPermissions.Entries.Read);

        group.MapAuditLogReadEndpoints();

        return group;
    }
}
