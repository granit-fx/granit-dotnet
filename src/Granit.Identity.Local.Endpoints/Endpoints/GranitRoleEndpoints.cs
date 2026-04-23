using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.Endpoints.Endpoints;

/// <summary>
/// Host / tenant admin CRUD endpoints for local roles (<see cref="RoleMetadata"/>).
/// </summary>
/// <remarks>
/// Visibility matrix applied server-side via <see cref="ICurrentTenant"/>:
/// <list type="bullet">
///   <item><b>Host admin</b>: full CRUD on Host + Both roles ; read-only view on Tenant roles.</item>
///   <item><b>Tenant admin</b>: read-only + assignable on Both roles ; full CRUD on own-tenant roles ; Host and other-tenant roles invisible.</item>
/// </list>
/// Non-visible roles surface as 404 on direct lookups — the endpoints never disclose
/// their existence by returning 403.
/// </remarks>
internal static class GranitRoleEndpoints
{
    internal static RouteGroupBuilder MapGranitRoleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListRoles")
            .WithSummary("Lists roles visible in the caller's context.")
            .WithDescription(
                "Host admins see every role (CRUD on Host / Both, read-only on tenant roles). "
                + "Tenant admins see Both roles (read-only) plus their own tenant roles (CRUD).")
            .Produces<IReadOnlyList<RoleResponse>>()
            .RequireAuthorization(IdentityLocalPermissions.Roles.Read);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetRole")
            .WithSummary("Returns a single role by identifier.")
            .WithDescription("Returns 404 when the role is not visible in the caller's context (no 403 to prevent leak).")
            .Produces<RoleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(IdentityLocalPermissions.Roles.Read);

        group.MapPost("/", CreateAsync)
            .WithName("CreateRole")
            .WithSummary("Creates a new local role and its RoleMetadata row.")
            .WithDescription(
                "Invariants: Host / Both ⇒ TenantId must be null; Tenant ⇒ TenantId required. "
                + "Side=Tenant requests are refused unless RoleEndpointsOptions.AllowTenantRoles is enabled.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<RoleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(IdentityLocalPermissions.Roles.Manage);

        group.MapPut("/{id:guid}", RenameAsync)
            .WithName("RenameRole")
            .WithSummary("Renames a role and updates its description.")
            .WithDescription("Side and tenant scope are immutable. System roles and non-visible roles cannot be renamed.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<RoleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(IdentityLocalPermissions.Roles.Manage);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteRole")
            .WithSummary("Hard-deletes a non-system role.")
            .WithDescription("System roles (SuperAdmin, TenantAdministrator, User) and non-visible roles cannot be deleted.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(IdentityLocalPermissions.Roles.Delete);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<RoleResponse>>> ListAsync(
        [FromServices] IRoleMetadataStore store,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleMetadata> all = await store.ListAllAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RoleResponse> visible = [.. all.Where(r => IsVisible(r, currentTenant)).Select(Map)];
        return TypedResults.Ok(visible);
    }

    private static async Task<Results<Ok<RoleResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IRoleMetadataStore store,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        RoleMetadata? role = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null || !IsVisible(role, currentTenant))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Role {id:D} not found.");
        }

        return TypedResults.Ok(Map(role));
    }

    private static async Task<Results<Created<RoleResponse>, ProblemHttpResult>> CreateAsync(
        [FromBody] RoleCreateRequest request,
        HttpContext httpContext,
        [FromServices] IGranitRoleOrchestrator orchestrator,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IOptions<RoleEndpointsOptions> options,
        CancellationToken cancellationToken)
    {
        RoleEndpointsOptions opts = options.Value;

        if (request.MultiTenancySide == MultiTenancySide.Tenant && !opts.AllowTenantRoles)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Tenant-scoped role creation is disabled. " +
                        "Set RoleEndpointsOptions.AllowTenantRoles = true to enable it.");
        }

        // Tenant admins may only create roles in their own tenant.
        if (currentTenant.IsAvailable && request.MultiTenancySide == MultiTenancySide.Tenant
            && request.TenantId != currentTenant.Id)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Tenant admins can only create roles within their own tenant.");
        }

        // Tenant admins cannot create Host or Both roles (platform-level scope).
        if (currentTenant.IsAvailable && request.MultiTenancySide != MultiTenancySide.Tenant)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Host and Both roles are managed from the host admin context.");
        }

        RoleMetadata created;
        try
        {
            created = await orchestrator.CreateAsync(
                new CreateRoleCommand(
                    Name: request.Name,
                    MultiTenancySide: request.MultiTenancySide,
                    TenantId: request.TenantId,
                    ClientId: null,
                    Description: request.Description,
                    IsSystem: false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }

        string basePath = httpContext.Request.Path.Value!.TrimEnd('/');
        return TypedResults.Created($"{basePath}/{created.Id:D}", Map(created));
    }

    private static async Task<Results<Ok<RoleResponse>, ProblemHttpResult>> RenameAsync(
        Guid id,
        [FromBody] RoleUpdateRequest request,
        [FromServices] IGranitRoleOrchestrator orchestrator,
        [FromServices] IRoleMetadataStore store,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        RoleMetadata? role = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null || !IsVisible(role, currentTenant))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Role {id:D} not found.");
        }

        if (!CanMutate(role, currentTenant))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have permission to modify this role.");
        }

        try
        {
            RoleMetadata updated = await orchestrator
                .RenameAsync(id, request.Name, request.Description, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(Map(updated));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("system role", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        [FromServices] IGranitRoleOrchestrator orchestrator,
        [FromServices] IRoleMetadataStore store,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        RoleMetadata? role = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null || !IsVisible(role, currentTenant))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Role {id:D} not found.");
        }

        if (!CanMutate(role, currentTenant))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have permission to delete this role.");
        }

        try
        {
            await orchestrator.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("system role", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    /// <summary>Applies the visibility matrix: Host invisible to tenant admins; other-tenant roles invisible.</summary>
    private static bool IsVisible(RoleMetadata role, ICurrentTenant currentTenant)
    {
        if (!currentTenant.IsAvailable)
        {
            // Host admin sees everything.
            return true;
        }

        // Tenant admin context.
        return role.MultiTenancySide switch
        {
            MultiTenancySide.Host => false,
            MultiTenancySide.Both => true,
            MultiTenancySide.Tenant => role.TenantId == currentTenant.Id,
            _ => false,
        };
    }

    /// <summary>
    /// Tenant admins cannot mutate Host or Both roles (read-only view). Host admins can mutate
    /// anything — Tenant-own editing cross-tenant is also permitted for platform support.
    /// </summary>
    private static bool CanMutate(RoleMetadata role, ICurrentTenant currentTenant)
    {
        if (!currentTenant.IsAvailable)
        {
            return true;
        }

        return role.MultiTenancySide == MultiTenancySide.Tenant
            && role.TenantId == currentTenant.Id;
    }

    private static RoleResponse Map(RoleMetadata role) =>
        new(role.Id,
            role.Name,
            role.MultiTenancySide,
            role.TenantId,
            role.ClientId,
            role.Description,
            role.IsSystem,
            role.CreatedAt,
            role.ModifiedAt);
}
