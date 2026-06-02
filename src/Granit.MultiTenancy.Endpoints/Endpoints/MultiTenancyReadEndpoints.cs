using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.MultiTenancy.Endpoints.Internal;
using Granit.MultiTenancy.Endpoints.Permissions;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.MultiTenancy.Endpoints.Endpoints;

/// <summary>
/// Read-only Minimal API endpoints for multi-tenancy management.
/// </summary>
internal static class MultiTenancyReadEndpoints
{
    /// <summary>Maps all tenant read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapMultiTenancyReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", GetByIdAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Read)
             .WithName("GetTenant")
             .WithSummary("Returns a tenant by ID.")
             .WithDescription("Fetches the full tenant details by unique identifier. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Read permission.")
             .Produces<TenantResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<TenantResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] ITenantReader reader,
        CancellationToken cancellationToken)
    {
        TenantData? tenant = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return TypedResults.Problem(
                detail: $"Tenant '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(MultiTenancyResponseMapper.ToResponse(tenant));
    }
}
