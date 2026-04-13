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
public static class AuditingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps read-only audit log endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditingEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>All endpoints are protected by the <c>Auditing.AuditEntries.Read</c> permission.</para>
    /// <para>Call from your application:</para>
    /// <code>
    /// app.MapGranitAuditing();
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapGranitAuditing(
        this IEndpointRouteBuilder endpoints,
        Action<AuditingEndpointsOptions>? configure = null)
    {
        AuditingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(AuditingPermissions.AuditEntries.Read);

        RouteGroupBuilder entriesGroup = group.MapGroup("audit-entries");
        entriesGroup.MapAuditingReadEndpoints();
        entriesGroup.MapAuditingManagementEndpoints();

        return group;
    }
}
