using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.Endpoints.Dtos;
using Granit.Entities.Customization.Endpoints.Internal;
using Granit.Entities.Customization.Endpoints.Permissions;
using Granit.Entities.Customization.Internal;
using Granit.Entities.Endpoints.Internal;
using Granit.Guids;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Entities.Customization.Endpoints.Endpoints;

/// <summary>
/// CRUD endpoints for the per-tenant Layer 1 customization on each
/// <c>EntityDefinition</c> layout. Read inherits the route group's
/// <c>EntitiesCustomization.Customizations.Read</c>; PUT and DELETE require
/// the <c>Manage</c> permission per ADR-053.
/// </summary>
internal static class EntityCustomizationEndpoints
{
    internal static RouteGroupBuilder MapEntityCustomizationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{entityName}/customization/{layoutKind}", GetAsync)
            .WithName("GetEntityCustomization")
            .WithSummary("Returns the tenant's customization for the given (entity, layout) pair.")
            .WithDescription("Returns 200 with the persisted deltas, or 404 when the tenant has not customized that layout (compiled defaults apply).")
            .Produces<EntityCustomizationResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{entityName}/customization/{layoutKind}", PutAsync)
            .RequireAuthorization(EntitiesCustomizationPermissions.Customizations.Manage)
            .WithName("PutEntityCustomization")
            .WithSummary("Replaces the tenant's customization for the given (entity, layout) pair.")
            .WithDescription("Full-replace semantics — the previous delta list is discarded. Validates each FieldName + group key against the compiled descriptor; rejects unknown names with 400. ISO 27001 audit entry written via IAuditingWriter on success.")
            .Produces<EntityCustomizationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{entityName}/customization/{layoutKind}", DeleteAsync)
            .RequireAuthorization(EntitiesCustomizationPermissions.Customizations.Manage)
            .WithName("DeleteEntityCustomization")
            .WithSummary("Reverts the tenant to the compiled defaults for the given (entity, layout) pair.")
            .WithDescription("Hard-deletes the persisted customization row. Idempotent — deleting a non-existent customization returns 204. ISO 27001 audit entry written on actual deletion only.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<Results<Ok<EntityCustomizationResponse>, NotFound>> GetAsync(
        [FromRoute] string entityName,
        [FromRoute] LayoutKind layoutKind,
        [FromServices] IEntityCustomizationReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        EntityCustomization? row = await reader.GetAsync(entityName, layoutKind, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToResponse(row));
    }

    private static async Task<Results<Ok<EntityCustomizationResponse>, ProblemHttpResult>> PutAsync(
        [FromRoute] string entityName,
        [FromRoute] LayoutKind layoutKind,
        [FromBody] EntityCustomizationRequest request,
        [FromServices] IEntityCustomizationReader reader,
        [FromServices] IEntityCustomizationWriter writer,
        [FromServices] DescriptorDeltaValidator descriptorValidator,
        [FromServices] EntityCustomizationAuditWriter auditWriter,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = descriptorValidator.Validate(entityName, layoutKind, request.Deltas);
        if (!validation.IsValid)
        {
            return TypedResults.Problem(
                detail: validation.Error,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Customization payload rejected against the compiled descriptor.");
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        EntityCustomization? existing = await reader.GetAsync(entityName, layoutKind, tenantId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LayoutDelta>? previousDeltas = existing?.Deltas;

        var toPersist = EntityCustomization.Create(
            id: existing?.Id ?? guidGenerator.Create(),
            entityName: entityName,
            layoutKind: layoutKind,
            deltas: request.Deltas,
            tenantId: tenantId);

        await writer.UpsertAsync(toPersist, cancellationToken).ConfigureAwait(false);
        await auditWriter.WriteUpsertAsync(toPersist, previousDeltas, cancellationToken).ConfigureAwait(false);
        await cache.RemoveByTagAsync(
            EntityCacheKey.EvictionTagForManifest(entityName), token: cancellationToken)
            .ConfigureAwait(false);

        EntityCustomization? saved = await reader.GetAsync(entityName, layoutKind, tenantId, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(ToResponse(saved!));
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] string entityName,
        [FromRoute] LayoutKind layoutKind,
        [FromServices] IEntityCustomizationReader reader,
        [FromServices] IEntityCustomizationWriter writer,
        [FromServices] EntityCustomizationAuditWriter auditWriter,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        EntityCustomization? existing = await reader.GetAsync(entityName, layoutKind, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.NoContent();
        }

        await writer.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        await auditWriter.WriteDeleteAsync(existing, cancellationToken).ConfigureAwait(false);
        await cache.RemoveByTagAsync(
            EntityCacheKey.EvictionTagForManifest(entityName), token: cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static EntityCustomizationResponse ToResponse(EntityCustomization row) =>
        new(row.Id, row.EntityName, row.LayoutKind, row.Deltas);
}
