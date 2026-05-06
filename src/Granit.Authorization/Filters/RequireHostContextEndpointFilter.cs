using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authorization.Filters;

/// <summary>
/// Endpoint filter that marks an endpoint as accessible from host context (no active tenant).
/// </summary>
/// <remarks>
/// <para>
/// When a tenant context is active, the filter is a no-op — the endpoint executes normally
/// with standard tenant-scoped data access. When no tenant context is active (host mode),
/// the filter verifies the caller is authenticated before proceeding.
/// </para>
/// <para>
/// Permission checks are handled by the existing <c>.RequireAuthorization(permission)</c>
/// chain which delegates to <see cref="IPermissionChecker"/> — this already uses
/// <c>perm:global:{role}:{permission}</c> in host context. This filter does NOT duplicate
/// that check; it only ensures no anonymous caller reaches host endpoints.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// group.MapGet("/{id:guid}", GetByIdAsync)
///     .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Read)
///     .AllowHostAccess();
/// </code>
/// </example>
internal sealed class RequireHostContextEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ICurrentTenant? currentTenant = context.HttpContext.RequestServices
            .GetService<ICurrentTenant>();

        // No multi-tenancy module or tenant is active — pass through.
        if (currentTenant is null || currentTenant.IsAvailable)
        {
            return next(context);
        }

        // Host mode — ensure caller is authenticated (defense in depth).
        // Permission checks are already handled by RequireAuthorization().
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return new ValueTask<object?>(
                TypedResults.Problem(
                    detail: "Authentication required for host-level access.",
                    statusCode: StatusCodes.Status401Unauthorized));
        }

        // Signal the data layer that the cross-tenant read about to happen is a
        // legitimate host-access path, not an accidental tenant-context loss.
        // EfStoreBase reads this feature to tag its metric origin=host_endpoint.
        context.HttpContext.Features.Set(IHostAccessFeature.HostMode);

        return next(context);
    }
}
