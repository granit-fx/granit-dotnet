using Granit.Guids;
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
/// Write Minimal API endpoints for multi-tenancy management (create, update, activate, deactivate).
/// </summary>
internal static class MultiTenancyWriteEndpoints
{
    /// <summary>Maps all tenant write endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapMultiTenancyWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Create)
             .WithName("CreateTenant")
             .WithSummary("Creates a new tenant.")
             .WithDescription("Creates a new active tenant with the specified name and identifier. The identifier must be unique, lowercase alphanumeric with hyphens (slug format). Returns the created tenant. Requires the MultiTenancy.Tenants.Create permission.")
             .Produces<TenantResponse>(StatusCodes.Status201Created)
             .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Update)
             .WithName("UpdateTenant")
             .WithSummary("Updates a tenant's details.")
             .WithDescription("Updates the display name and contact email of an existing tenant. Returns 404 if the tenant does not exist. Returns 409 if the concurrency stamp does not match the stored value. Requires the MultiTenancy.Tenants.Update permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesValidationProblem();

        group.MapPost("/{id:guid}/activate", ActivateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Manage)
             .WithName("ActivateTenant")
             .WithSummary("Activates a tenant.")
             .WithDescription("Activates a previously deactivated tenant, allowing it to be resolved by the tenant resolution middleware. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deactivate", DeactivateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Manage)
             .WithName("DeactivateTenant")
             .WithSummary("Deactivates a tenant.")
             .WithDescription("Deactivates a tenant, preventing it from being resolved by the tenant resolution middleware. Raises a TenantDeactivatedEvent that can trigger session invalidation. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Created<TenantResponse>, ProblemHttpResult>> CreateAsync(
        CreateTenantRequest body,
        [FromServices] ITenantWriter writer,
        [FromServices] ITenantReader reader,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Guid id = guidGenerator.Create();

        await writer
            .CreateAsync(id, body.Name, body.Identifier, body.ContactEmail, body.Jurisdiction, cancellationToken)
            .ConfigureAwait(false);

        TenantData? tenant = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created($"/{id}", MultiTenancyResponseMapper.ToResponse(tenant!));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateTenantRequest body,
        [FromServices] ITenantWriter writer,
        [FromServices] ITenantReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return TenantNotFound(id);
        }

        await writer
            .UpdateAsync(id, body.Name, body.ContactEmail, body.Jurisdiction, body.ConcurrencyStamp, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ActivateAsync(
        Guid id,
        [FromServices] ITenantWriter writer,
        [FromServices] ITenantReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return TenantNotFound(id);
        }

        await writer
            .ActivateAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeactivateAsync(
        Guid id,
        [FromServices] ITenantWriter writer,
        [FromServices] ITenantReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return TenantNotFound(id);
        }

        await writer
            .DeactivateAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static ProblemHttpResult TenantNotFound(Guid id) =>
        TypedResults.Problem(
            detail: $"Tenant '{id}' was not found.",
            statusCode: StatusCodes.Status404NotFound);
}
