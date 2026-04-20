using Granit.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authorization.Extensions;

/// <summary>
/// Extension methods for configuring host-level access on Minimal API endpoints.
/// </summary>
public static class HostAccessEndpointExtensions
{
    /// <summary>
    /// Marks the endpoint as accessible from host context (no active tenant) in addition
    /// to normal tenant-scoped access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Endpoints without this marker only serve tenant-scoped data. In host context
    /// (no <c>X-Tenant-Id</c> header), the multi-tenant query filter produces
    /// <c>WHERE TenantId IS NULL</c> which returns no tenant data — a safe default.
    /// </para>
    /// <para>
    /// Adding <c>.AllowHostAccess()</c> enables the endpoint to serve cross-tenant data
    /// by bypassing the multi-tenant query filter. Permission checks are handled by the
    /// existing <c>.RequireAuthorization(permission)</c> — no need to repeat the permission.
    /// </para>
    /// </remarks>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The <see cref="RouteHandlerBuilder"/> for further chaining.</returns>
    public static RouteHandlerBuilder AllowHostAccess(
        this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new RequireHostContextEndpointFilter());

    /// <summary>
    /// Marks every endpoint in the route group as accessible from host context. Equivalent
    /// to calling <see cref="AllowHostAccess(RouteHandlerBuilder)"/> on each handler.
    /// </summary>
    public static RouteGroupBuilder AllowHostAccess(
        this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(new RequireHostContextEndpointFilter());
        return group;
    }
}
