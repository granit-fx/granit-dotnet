using Granit.Guids;
using Granit.Querying;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.ReferenceData.Endpoints.Endpoints;

/// <summary>
/// Endpoints for dynamically registered reference data types (non-generic, keyed services).
/// </summary>
internal static class ReferenceDataDynamicEndpoints
{
    /// <summary>
    /// Registers GET / and GET /{code} for a dynamic reference data type.
    /// </summary>
    internal static RouteGroupBuilder MapDynamicReadEndpoints(
        this RouteGroupBuilder group,
        string typeName)
    {
        group.MapGet("/", async (
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);

            ReferenceDataQueryParameters parameters = new();
            ReferenceDataQuery query = new(
                ActiveOnly: parameters.ActiveOnly,
                SearchTerm: parameters.Search,
                SortBy: parameters.SortBy,
                Descending: parameters.Descending,
                Page: parameters.Page,
                PageSize: parameters.PageSize);

            PagedResult<DynamicReferenceDataEntity> result = await reader.GetAllAsync(query, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(result);
        })
        .WithName($"GetAll{typeName}")
        .WithSummary($"Returns a filtered, paginated list of {typeName} entries.")
        .WithDescription($"Lists {typeName} reference data entries with support for filtering (active-only, search term), sorting, and pagination.")
        .Produces<PagedResult<DynamicReferenceDataEntity>>();

        group.MapGet("/{code}", async (
            string code,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);

            DynamicReferenceDataEntity? entity = await reader.GetByCodeAsync(code, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(entity);
        })
        .WithName($"Get{typeName}ByCode")
        .WithSummary($"Returns a single {typeName} entry by code.")
        .WithDescription($"Returns the full {typeName} entry identified by its unique code. Returns 404 if not found.")
        .Produces<DynamicReferenceDataEntity>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Registers POST /, PUT /{code}, and DELETE /{code} for a dynamic reference data type.
    /// </summary>
    internal static RouteGroupBuilder MapDynamicAdminEndpoints(
        this RouteGroupBuilder group,
        string typeName,
        string? adminPolicyName)
    {
        RouteGroupBuilder adminGroup = group.MapGroup("/");

        if (adminPolicyName is not null)
        {
            adminGroup.RequireAuthorization(adminPolicyName);
        }

        adminGroup.MapPost("/", async (
            ReferenceDataCreateRequest request,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IReferenceDataStoreWriter<DynamicReferenceDataEntity> writer =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreWriter<DynamicReferenceDataEntity>>(typeName);
            IGuidGenerator guidGenerator = httpContext.RequestServices.GetRequiredService<IGuidGenerator>();

            DynamicReferenceDataEntity entity = new()
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

            await writer.CreateAsync(entity, cancellationToken).ConfigureAwait(false);
            return TypedResults.Created($"{request.Code}");
        })
        .WithName($"Create{typeName}")
        .WithSummary($"Creates a new {typeName} entry.")
        .WithDescription($"Creates a new {typeName} reference data entry with a unique code and localized labels.")
        .Produces(StatusCodes.Status201Created);

        adminGroup.MapPut("/{code}", async (
            string code,
            ReferenceDataUpdateRequest request,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);
            IReferenceDataStoreWriter<DynamicReferenceDataEntity> writer =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreWriter<DynamicReferenceDataEntity>>(typeName);

            DynamicReferenceDataEntity? existing = await reader.GetByCodeAsync(code, cancellationToken)
                .ConfigureAwait(false);

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

            await writer.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok();
        })
        .WithName($"Update{typeName}")
        .WithSummary($"Updates an existing {typeName} entry.")
        .WithDescription($"Updates labels, sort order, active status, and validity dates. Returns 404 if not found.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapDelete("/{code}", async (
            string code,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);
            IReferenceDataStoreWriter<DynamicReferenceDataEntity> writer =
                httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreWriter<DynamicReferenceDataEntity>>(typeName);

            DynamicReferenceDataEntity? existing = await reader.GetByCodeAsync(code, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return TypedResults.NotFound();
            }

            await writer.SetActiveAsync(code, false, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        })
        .WithName($"Deactivate{typeName}")
        .WithSummary($"Deactivates a {typeName} entry (soft delete).")
        .WithDescription($"Sets the entry's active flag to false. Returns 404 if not found.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
