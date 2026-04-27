using Granit.Guids;
using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.MultiTenancy.Endpoints.Options;
using Granit.MultiTenancy.Endpoints.Permissions;
using Granit.MultiTenancy.Stores;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.MultiTenancy.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping multi-tenancy management endpoints.
/// </summary>
public static class MultiTenancyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps multi-tenancy management endpoints under <c>/{prefix}/tenants</c>
    /// (default prefix: <c>multi-tenancy</c>, so the full path is <c>/multi-tenancy/tenants</c>).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="MultiTenancyEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitMultiTenancy(
        this IEndpointRouteBuilder endpoints,
        Action<MultiTenancyEndpointsOptions>? configure = null)
    {
        MultiTenancyEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        RouteGroupBuilder tenantsGroup = group.MapGranitGroup("tenants");

        // Tenant listing is intentionally not mapped here — consumers register a Granit.QueryEngine
        // endpoint at the same prefix to keep Granit.MultiTenancy.Endpoints decoupled from EF Core
        // and the query engine. See the multi-tenancy module reference docs for the recipe.
        MapGetByIdEndpoint(tenantsGroup);
        MapCreateEndpoint(tenantsGroup);
        MapUpdateEndpoint(tenantsGroup);
        MapActivateEndpoint(tenantsGroup);
        MapDeactivateEndpoint(tenantsGroup);

        return group;
    }

    // -------------------------------------------------------------------------
    // GET /{id:guid} — Get tenant by ID
    // -------------------------------------------------------------------------

    private static void MapGetByIdEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleGetByIdAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Read)
             .WithName("GetTenant")
             .WithSummary("Returns a tenant by ID.")
             .WithDescription("Fetches the full tenant details by unique identifier. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Read permission.")
             .Produces<TenantResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // POST / — Create tenant
    // -------------------------------------------------------------------------

    private static void MapCreateEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/", HandleCreateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Create)
             .WithName("CreateTenant")
             .WithSummary("Creates a new tenant.")
             .WithDescription("Creates a new active tenant with the specified name and identifier. The identifier must be unique, lowercase alphanumeric with hyphens (slug format). Returns the created tenant. Requires the MultiTenancy.Tenants.Create permission.")
             .Produces<TenantResponse>(StatusCodes.Status201Created)
             .ProducesValidationProblem();
    }

    // -------------------------------------------------------------------------
    // PUT /{id:guid} — Update tenant
    // -------------------------------------------------------------------------

    private static void MapUpdateEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleUpdateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Update)
             .WithName("UpdateTenant")
             .WithSummary("Updates a tenant's details.")
             .WithDescription("Updates the display name and contact email of an existing tenant. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Update permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesValidationProblem();
    }

    // -------------------------------------------------------------------------
    // POST /{id:guid}/activate — Activate tenant
    // -------------------------------------------------------------------------

    private static void MapActivateEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/activate", HandleActivateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Manage)
             .WithName("ActivateTenant")
             .WithSummary("Activates a tenant.")
             .WithDescription("Activates a previously deactivated tenant, allowing it to be resolved by the tenant resolution middleware. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // POST /{id:guid}/deactivate — Deactivate tenant
    // -------------------------------------------------------------------------

    private static void MapDeactivateEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/deactivate", HandleDeactivateAsync)
             .RequireAuthorization(MultiTenancyPermissions.Tenants.Manage)
             .WithName("DeactivateTenant")
             .WithSummary("Deactivates a tenant.")
             .WithDescription("Deactivates a tenant, preventing it from being resolved by the tenant resolution middleware. Raises a TenantDeactivatedEvent that can trigger session invalidation. Returns 404 if the tenant does not exist. Requires the MultiTenancy.Tenants.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TenantResponse>, ProblemHttpResult>> HandleGetByIdAsync(
        Guid id,
        [FromServices] ITenantReader reader,
        CancellationToken cancellationToken)
    {
        TenantData? tenant = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return TenantNotFound(id);
        }

        return TypedResults.Ok(ToResponse(tenant));
    }

    private static async Task<Results<Created<TenantResponse>, ProblemHttpResult>> HandleCreateAsync(
        CreateTenantRequest body,
        [FromServices] ITenantWriter writer,
        [FromServices] ITenantReader reader,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Guid id = guidGenerator.Create();

        await writer
            .CreateAsync(id, body.Name, body.Identifier, body.PartyEmail, body.Jurisdiction, cancellationToken)
            .ConfigureAwait(false);

        TenantData? tenant = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created($"/{id}", ToResponse(tenant!));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleUpdateAsync(
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
            .UpdateAsync(id, body.Name, body.PartyEmail, body.Jurisdiction, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleActivateAsync(
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

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeactivateAsync(
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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TenantResponse ToResponse(TenantData data) =>
        new(data.Id, data.Name, data.Identifier, data.PartyEmail, data.Activated, data.Jurisdiction, data.CreatedAt);

    private static ProblemHttpResult TenantNotFound(Guid id) =>
        TypedResults.Problem(
            detail: $"Tenant '{id}' was not found.",
            statusCode: StatusCodes.Status404NotFound);
}
