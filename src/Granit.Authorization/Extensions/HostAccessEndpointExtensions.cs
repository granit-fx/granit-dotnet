using Granit.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Granit.Authorization.Extensions;

/// <summary>
/// Extension methods for configuring host-level access on Minimal API endpoints.
/// </summary>
public static class HostAccessEndpointExtensions
{
    /// <summary>
    /// Marks the endpoint as accessible from host context (no active tenant) in addition
    /// to normal tenant-scoped access. When the caller has no tenant context, the specified
    /// <paramref name="hostPermission"/> is checked before the handler executes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Endpoints without this filter only serve tenant-scoped data. In host context
    /// (no <c>X-Tenant-Id</c> header), the multi-tenant query filter produces
    /// <c>WHERE TenantId IS NULL</c> which returns no tenant data — a safe default.
    /// </para>
    /// <para>
    /// Adding <c>.AllowHostAccess()</c> enables the endpoint to serve cross-tenant data
    /// by bypassing the multi-tenant query filter, but <b>only after verifying</b> the
    /// caller has the required host-level permission.
    /// </para>
    /// </remarks>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="hostPermission">
    /// The permission name to check in host context (e.g. <c>"Subscriptions.Subscriptions.Read"</c>).
    /// </param>
    /// <returns>The <see cref="RouteHandlerBuilder"/> for further chaining.</returns>
    public static RouteHandlerBuilder AllowHostAccess(
        this RouteHandlerBuilder builder, string hostPermission) =>
        builder.AddEndpointFilter(
            new RequireHostContextEndpointFilter(hostPermission));
}
