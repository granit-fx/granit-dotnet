using Granit.Domain;
using Granit.Guids;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Internal;
using Granit.ReferenceData.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.ReferenceData.Endpoints.Endpoints;

/// <summary>
/// Admin endpoints for managing reference data entries (create, update, deactivate).
/// </summary>
internal static class ReferenceDataAdminEndpoints
{
    /// <summary>
    /// Registers POST /, PUT /{code}, and DELETE /{code} onto the given route group.
    /// Admin endpoints are protected by a scope-based filter: Global types require host context,
    /// Tenant types require tenant context.
    /// </summary>
    internal static RouteGroupBuilder MapAdminEndpoints<TEntity>(
        this RouteGroupBuilder group,
        ReferenceDataScope scope = ReferenceDataScope.Global)
        where TEntity : ReferenceDataEntity, new()
    {
        ReferenceDataScopeEndpointFilter scopeFilter = new(scope);

        group.MapPost("/", CreateAsync<TEntity>)
            .AddEndpointFilter(scopeFilter)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Manage)
            .WithName($"Create{typeof(TEntity).Name}")
            .WithSummary($"Creates a new {typeof(TEntity).Name} entry.")
            .WithDescription($"Creates a new {typeof(TEntity).Name} reference data entry with a unique code and localized labels for all supported languages. The entry is active by default. Optional validity date range can restrict when the entry is selectable.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        group.MapPut("/{code}", UpdateAsync<TEntity>)
            .AddEndpointFilter(scopeFilter)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Manage)
            .WithName($"Update{typeof(TEntity).Name}")
            .WithSummary($"Updates an existing {typeof(TEntity).Name} entry.")
            .WithDescription($"Updates the labels, sort order, active status, and validity dates of an existing {typeof(TEntity).Name} entry. The code is immutable and cannot be changed. ExtraProperties use merge semantics: properties in the request are added or updated, properties not in the request are preserved. Returns 404 if no entry matches the code.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        group.MapDelete("/{code}", DeactivateAsync<TEntity>)
            .AddEndpointFilter(scopeFilter)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Manage)
            .WithName($"Deactivate{typeof(TEntity).Name}")
            .WithSummary($"Deactivates a {typeof(TEntity).Name} entry (soft delete).")
            .WithDescription($"Sets the entry's active flag to false. Deactivated entries are excluded from default queries but remain in the database for referential integrity. Returns 404 if no entry matches the code.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<Created> CreateAsync<TEntity>(
        ReferenceDataCreateRequest request,
        [FromServices] IReferenceDataStoreWriter<TEntity> storeWriter,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity, new()
    {
        TEntity entity = new()
        {
            Id = guidGenerator.Create(),
            Code = request.Code,
            LabelEn = request.LabelEn,
            LabelFr = request.LabelFr,
            LabelNl = request.LabelNl,
            LabelDe = request.LabelDe,
            LabelEs = request.LabelEs,
            LabelIt = request.LabelIt,
            LabelPt = request.LabelPt,
            LabelZh = request.LabelZh,
            LabelJa = request.LabelJa,
            LabelPl = request.LabelPl,
            LabelTr = request.LabelTr,
            LabelKo = request.LabelKo,
            LabelSv = request.LabelSv,
            LabelCs = request.LabelCs,
            LabelHi = request.LabelHi,
            SortOrder = request.SortOrder,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            ParentCode = request.ParentCode,
            IsActive = true,
        };

        if (request.ExtraProperties is { Count: > 0 })
        {
            foreach ((string key, string value) in request.ExtraProperties)
            {
                entity.SetExtraProperty(key, value);
            }
        }

        await storeWriter.CreateAsync(entity, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"{request.Code}");
    }

    private static async Task<Results<Ok, ProblemHttpResult>> UpdateAsync<TEntity>(
        string code,
        ReferenceDataUpdateRequest request,
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        [FromServices] IReferenceDataStoreWriter<TEntity> storeWriter,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity, new()
    {
        TEntity? existing = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        existing.LabelEn = request.LabelEn;
        existing.LabelFr = request.LabelFr;
        existing.LabelNl = request.LabelNl;
        existing.LabelDe = request.LabelDe;
        existing.LabelEs = request.LabelEs;
        existing.LabelIt = request.LabelIt;
        existing.LabelPt = request.LabelPt;
        existing.LabelZh = request.LabelZh;
        existing.LabelJa = request.LabelJa;
        existing.LabelPl = request.LabelPl;
        existing.LabelTr = request.LabelTr;
        existing.LabelKo = request.LabelKo;
        existing.LabelSv = request.LabelSv;
        existing.LabelCs = request.LabelCs;
        existing.LabelHi = request.LabelHi;
        existing.SortOrder = request.SortOrder;
        existing.IsActive = request.IsActive;
        existing.ValidFrom = request.ValidFrom;
        existing.ValidTo = request.ValidTo;
        existing.ParentCode = request.ParentCode;

        if (request.ExtraProperties is not null)
        {
            foreach ((string key, string value) in request.ExtraProperties)
            {
                existing.SetExtraProperty(key, value);
            }
        }

        await storeWriter.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeactivateAsync<TEntity>(
        string code,
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        [FromServices] IReferenceDataStoreWriter<TEntity> storeWriter,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity, new()
    {
        TEntity? existing = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        await storeWriter.SetActiveAsync(code, false, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
