using Granit.Guids;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
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
    /// </summary>
    internal static RouteGroupBuilder MapAdminEndpoints<TEntity>(
        this RouteGroupBuilder group,
        string? adminPolicyName)
        where TEntity : ReferenceDataEntity, new()
    {
        RouteGroupBuilder adminGroup = group.MapGroup("/");

        if (adminPolicyName is not null)
        {
            adminGroup.RequireAuthorization(adminPolicyName);
        }

        adminGroup.MapPost("/", CreateAsync<TEntity>)
            .WithName($"Create{typeof(TEntity).Name}")
            .WithSummary($"Creates a new {typeof(TEntity).Name} entry.")
            .WithDescription($"Creates a new {typeof(TEntity).Name} reference data entry with a unique code and localized labels for all supported languages. The entry is active by default. Optional validity date range can restrict when the entry is selectable.");

        adminGroup.MapPut("/{code}", UpdateAsync<TEntity>)
            .WithName($"Update{typeof(TEntity).Name}")
            .WithSummary($"Updates an existing {typeof(TEntity).Name} entry.")
            .WithDescription($"Updates the labels, sort order, active status, and validity dates of an existing {typeof(TEntity).Name} entry. The code is immutable and cannot be changed. Returns 404 if no entry matches the code.");

        adminGroup.MapDelete("/{code}", DeactivateAsync<TEntity>)
            .WithName($"Deactivate{typeof(TEntity).Name}")
            .WithSummary($"Deactivates a {typeof(TEntity).Name} entry (soft delete).")
            .WithDescription($"Sets the entry's active flag to false. Deactivated entries are excluded from default queries but remain in the database for referential integrity. Returns 404 if no entry matches the code.");

        return group;
    }

    private static async Task<Created> CreateAsync<TEntity>(
        ReferenceDataCreateRequest request,
        IReferenceDataStoreWriter<TEntity> storeWriter,
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
            SortOrder = request.SortOrder,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IsActive = true,
        };

        await storeWriter.CreateAsync(entity, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"{request.Code}");
    }

    private static async Task<Results<Ok, NotFound>> UpdateAsync<TEntity>(
        string code,
        ReferenceDataUpdateRequest request,
        IReferenceDataStoreReader<TEntity> storeReader,
        IReferenceDataStoreWriter<TEntity> storeWriter,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity, new()
    {
        TEntity? existing = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.NotFound();
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
        existing.SortOrder = request.SortOrder;
        existing.IsActive = request.IsActive;
        existing.ValidFrom = request.ValidFrom;
        existing.ValidTo = request.ValidTo;

        await storeWriter.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok();
    }

    private static async Task<Results<NoContent, NotFound>> DeactivateAsync<TEntity>(
        string code,
        IReferenceDataStoreReader<TEntity> storeReader,
        IReferenceDataStoreWriter<TEntity> storeWriter,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity, new()
    {
        TEntity? existing = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        await storeWriter.SetActiveAsync(code, false, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
