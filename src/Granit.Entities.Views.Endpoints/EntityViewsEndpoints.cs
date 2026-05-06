using Granit.Entities.Views.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Entities.Views.Endpoints;

/// <summary>
/// Minimal API handlers for the EntityView surface (per ADR-047 §6).
/// Routes are mounted under <c>/entities/{entityName}/views</c> by
/// <c>MapGranitEntityViewsEndpoints</c>.
/// </summary>
internal static class EntityViewsEndpoints
{
    public static RouteGroupBuilder MapEntityViewsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListEntityViews")
            .WithSummary("Lists every saved view accessible to the current user for the given entity.")
            .WithDescription("Returns Personal views owned by the user, Shared views whose audience targets the user (by role or user id), and every Tenant view in the current tenant. Sorted by SortOrder ascending then Name.")
            .Produces<IReadOnlyList<EntityViewResponse>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetEntityView")
            .WithSummary("Gets one saved view by id.")
            .WithDescription("Returns 404 when the view does not exist or is not accessible to the current user (defense-in-depth: same response shape, no scope leakage).")
            .Produces<EntityViewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/_default", GetDefaultAsync)
            .WithName("GetEntityDefaultView")
            .WithSummary("Resolves the user's effective default saved view for the entity.")
            .WithDescription("Precedence per ADR-047 §4: IsPersonalDefault > IsDefault > compiled fallback. Returns 204 when no saved or pinned view exists — the renderer falls back to the compiled default collection.")
            .Produces<EntityViewResponse>()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(EntityViewPermissions.Create)
            .WithName("CreateEntityView")
            .WithSummary("Creates a Personal saved view for the current user.")
            .WithDescription("Requires the Entities.Views.Create permission. The created view is owned by the caller and bound to the supplied compiled collection (BasedOn) and kind. Returns 201 with the created descriptor.")
            .Produces<EntityViewResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(EntityViewPermissions.Manage)
            .WithName("UpdateEntityView")
            .WithSummary("Updates the name / description / icon / state of a saved view.")
            .WithDescription("Requires ownership for Personal / Shared views, or Entities.Views.Manage for Tenant views. BasedOn and Kind are immutable post-creation and rejected if changed.")
            .Produces<EntityViewResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(EntityViewPermissions.DeleteAny)
            .WithName("DeleteEntityView")
            .WithSummary("Deletes a saved view.")
            .WithDescription("The owner can delete their own views; moderation deletes require Entities.Views.DeleteAny. Returns 204 on success.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/pin", SetPinnedAsync)
            .RequireAuthorization(EntityViewPermissions.Manage)
            .WithName("ToggleEntityViewPinned")
            .WithSummary("Pins or unpins a saved view in the workspace tab strip.")
            .WithDescription("Requires Entities.Views.Manage. The pinned flag is independent from IsDefault / IsPersonalDefault.")
            .Produces<EntityViewResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/set-default", SetTenantDefaultAsync)
            .RequireAuthorization(EntityViewPermissions.Manage)
            .WithName("ToggleEntityViewTenantDefault")
            .WithSummary("Sets or clears the tenant-default flag.")
            .WithDescription("Requires Entities.Views.Manage. At most one IsDefault view per (entity, tenant) — setting one clears any existing tenant default.")
            .Produces<EntityViewResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/star", SetPersonalDefaultAsync)
            .WithName("ToggleEntityViewPersonalDefault")
            .WithSummary("Sets or clears the personal-default flag for the current user.")
            .WithDescription("Requires the caller to own the view (group-level Read permission suffices — ownership is enforced in the writer). At most one IsPersonalDefault view per (entity, user) — setting one clears any existing personal default.")
            .Produces<EntityViewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/share", ShareAsync)
            .RequireAuthorization(EntityViewPermissions.Share)
            .WithName("ShareEntityView")
            .WithSummary("Promotes a Personal view to Shared with the given audience.")
            .WithDescription("Requires Entities.Views.Share. The audience must include at least one role or user — empty audiences are rejected.")
            .Produces<EntityViewResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<EntityViewResponse>>> ListAsync(
        string entityName,
        [FromServices] IEntityViewReader reader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<EntityViewDescriptor> views = await reader.ListAsync(entityName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EntityViewResponse> response = [.. views.Select(EntityViewResponse.FromDescriptor)];
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> GetByIdAsync(
        string entityName,
        Guid id,
        [FromServices] IEntityViewReader reader,
        CancellationToken cancellationToken)
    {
        EntityViewDescriptor? view = await reader.GetAsync(entityName, id, cancellationToken).ConfigureAwait(false);
        return view is null
            ? TypedResults.Problem(detail: "Entity view not found.", statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(EntityViewResponse.FromDescriptor(view));
    }

    private static async Task<Results<Ok<EntityViewResponse>, NoContent>> GetDefaultAsync(
        string entityName,
        [FromServices] IEntityViewReader reader,
        CancellationToken cancellationToken)
    {
        EntityViewDescriptor? view = await reader.GetDefaultViewAsync(entityName, cancellationToken).ConfigureAwait(false);
        return view is null
            ? TypedResults.NoContent()
            : TypedResults.Ok(EntityViewResponse.FromDescriptor(view));
    }

    private static async Task<Created<EntityViewResponse>> CreateAsync(
        string entityName,
        EntityViewCreateBodyRequest body,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken)
    {
        EntityViewDescriptor created = await writer.CreateAsync(
            new EntityViewCreateRequest(
                EntityName: entityName,
                BasedOn: body.BasedOn,
                Kind: body.Kind,
                Name: body.Name,
                Description: body.Description,
                Icon: body.Icon,
                State: body.State),
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/entities/{entityName}/views/{created.Id}",
            EntityViewResponse.FromDescriptor(created));
    }

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> UpdateAsync(
        string entityName,
        Guid id,
        EntityViewUpdateBodyRequest body,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            EntityViewDescriptor updated = await writer.UpdateAsync(
                id,
                new EntityViewUpdateRequest(body.Name, body.Description, body.Icon, body.State),
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(EntityViewResponse.FromDescriptor(updated));
        }
        catch (EntityViewNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string entityName,
        Guid id,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (EntityViewNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> SetPinnedAsync(
        string entityName,
        Guid id,
        EntityViewToggleFlagRequest body,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken) =>
        await ToggleAsync(id, body, writer.SetPinnedAsync, cancellationToken).ConfigureAwait(false);

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> SetTenantDefaultAsync(
        string entityName,
        Guid id,
        EntityViewToggleFlagRequest body,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken) =>
        await ToggleAsync(id, body, writer.SetTenantDefaultAsync, cancellationToken).ConfigureAwait(false);

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> SetPersonalDefaultAsync(
        string entityName,
        Guid id,
        EntityViewToggleFlagRequest body,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken) =>
        await ToggleAsync(id, body, writer.SetPersonalDefaultAsync, cancellationToken).ConfigureAwait(false);

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> ShareAsync(
        string entityName,
        Guid id,
        EntityViewShareBodyRequest body,
        [FromServices] IEntityViewWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            EntityViewDescriptor updated = await writer.ShareAsync(
                id,
                new EntityViewSharedWith(body.Roles, body.Users),
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(EntityViewResponse.FromDescriptor(updated));
        }
        catch (EntityViewNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<Results<Ok<EntityViewResponse>, ProblemHttpResult>> ToggleAsync(
        Guid id,
        EntityViewToggleFlagRequest body,
        Func<Guid, bool, CancellationToken, Task<EntityViewDescriptor>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            EntityViewDescriptor updated = await action(id, body.Value, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(EntityViewResponse.FromDescriptor(updated));
        }
        catch (EntityViewNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}
