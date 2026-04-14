using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authorization.Filters;

/// <summary>
/// Endpoint filter that validates host-level authorization on endpoints supporting
/// cross-tenant administration.
/// </summary>
/// <remarks>
/// <para>
/// Apply this filter to endpoints that should be accessible from both tenant and host
/// contexts. When a tenant context is active, the filter is a no-op — the endpoint
/// executes normally with standard tenant-scoped data access. When no tenant context
/// is active (host mode), the filter verifies the caller has the specified host-level
/// permission before proceeding.
/// </para>
/// <para>
/// This implements <b>defense in depth</b>: even if the data layer bypasses the
/// multi-tenant query filter in host context, unauthorized callers are rejected here
/// before reaching the handler.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// group.MapGet("/{id:guid}", GetByIdAsync)
///     .AllowHostAccess(SubscriptionsPermissions.Subscriptions.Read);
/// </code>
/// </example>
internal sealed class RequireHostContextEndpointFilter(
    string hostPermission) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ICurrentTenant currentTenant = context.HttpContext.RequestServices
            .GetRequiredService<ICurrentTenant>();

        // Tenant mode — always allowed, no extra check needed.
        if (currentTenant.IsAvailable)
        {
            return await next(context).ConfigureAwait(false);
        }

        // Host mode — verify caller has host-level permission.
        IPermissionChecker checker = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionChecker>();

        if (!await checker.IsGrantedAsync(hostPermission).ConfigureAwait(false))
        {
            return TypedResults.Problem(
                detail: "Host-level authorization required for this operation.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}
