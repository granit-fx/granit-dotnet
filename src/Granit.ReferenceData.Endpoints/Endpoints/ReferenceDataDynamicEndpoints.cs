using Granit.Domain;
using Granit.Guids;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Internal;
using Granit.ReferenceData.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.ReferenceData.Endpoints.Endpoints;

/// <summary>
/// Endpoints for dynamically registered reference data types (non-generic, keyed services).
/// Stores the type name in endpoint metadata and resolves keyed services at request time.
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
        group.MapGet("/", GetAllAsync)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
            .WithName($"GetAll{typeName}")
            .WithSummary($"Returns a filtered, paginated list of {typeName} entries.")
            .WithDescription($"Lists {typeName} reference data entries with support for filtering, sorting, and pagination.")
            .Produces<PagedResult<ReferenceDataResponse>>()
            .WithMetadata(new ReferenceDataTypeNameMetadata(typeName));

        group.MapGet("/{code}", GetByCodeAsync)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
            .WithName($"Get{typeName}ByCode")
            .WithSummary($"Returns a single {typeName} entry by code.")
            .WithDescription($"Returns the full {typeName} entry identified by its unique code. Returns 404 if not found.")
            .Produces<ReferenceDataResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithMetadata(new ReferenceDataTypeNameMetadata(typeName));

        group.MapGet("/{code}/children", GetChildrenAsync)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
            .WithName($"Get{typeName}Children")
            .WithSummary($"Returns direct children of a {typeName} entry.")
            .WithDescription($"Returns all active direct children ordered by sort order. Returns 404 if the parent does not exist.")
            .Produces<IReadOnlyList<ReferenceDataResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithMetadata(new ReferenceDataTypeNameMetadata(typeName));

        return group;
    }

    /// <summary>
    /// Registers POST /, PUT /{code}, and DELETE /{code} for a dynamic reference data type.
    /// Admin endpoints are protected by a scope-based filter.
    /// </summary>
    internal static RouteGroupBuilder MapDynamicAdminEndpoints(
        this RouteGroupBuilder group,
        string typeName,
        ReferenceDataScope scope = ReferenceDataScope.Global)
    {
        ReferenceDataScopeEndpointFilter scopeFilter = new(scope);

        group.MapPost("/", CreateAsync)
            .AddEndpointFilter(scopeFilter)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Create)
            .WithName($"Create{typeName}")
            .WithSummary($"Creates a new {typeName} entry.")
            .WithDescription($"Creates a new {typeName} reference data entry with a unique code and localized labels.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithMetadata(new ReferenceDataTypeNameMetadata(typeName));

        group.MapPut("/{code}", UpdateAsync)
            .AddEndpointFilter(scopeFilter)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Manage)
            .WithName($"Update{typeName}")
            .WithSummary($"Updates an existing {typeName} entry.")
            .WithDescription($"Updates labels, sort order, active status, and validity dates. ExtraProperties use merge semantics: properties in the request are added or updated, properties not in the request are preserved. Returns 404 if not found.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithMetadata(new ReferenceDataTypeNameMetadata(typeName));

        group.MapDelete("/{code}", DeactivateAsync)
            .AddEndpointFilter(scopeFilter)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Manage)
            .WithName($"Deactivate{typeName}")
            .WithSummary($"Deactivates a {typeName} entry (soft delete).")
            .WithDescription($"Sets the entry's active flag to false. Returns 404 if not found.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithMetadata(new ReferenceDataTypeNameMetadata(typeName));

        return group;
    }

    // ──── Handlers ────

    private static string ResolveTypeName(HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<ReferenceDataTypeNameMetadata>()?.TypeName
        ?? throw new InvalidOperationException("ReferenceDataTypeNameMetadata not found on endpoint.");

    private static async Task<Ok<PagedResult<ReferenceDataResponse>>> GetAllAsync(
        [AsParameters] ReferenceDataQueryParameters parameters,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string typeName = ResolveTypeName(httpContext);
        IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);

        ReferenceDataQuery query = new(
            ActiveOnly: parameters.ActiveOnly,
            SearchTerm: parameters.Search,
            SortBy: parameters.SortBy,
            Descending: parameters.Descending,
            Page: parameters.Page,
            PageSize: parameters.PageSize);

        PagedResult<DynamicReferenceDataEntity> result = await reader
            .GetAllAsync(query, cancellationToken).ConfigureAwait(false);

        PagedResult<ReferenceDataResponse> mapped = new(
            result.Items.Select(ReferenceDataMapper.ToResponse).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(mapped);
    }

    private static async Task<Results<Ok<ReferenceDataResponse>, NotFound>> GetByCodeAsync(
        string code,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string typeName = ResolveTypeName(httpContext);
        IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);

        DynamicReferenceDataEntity? entity = await reader
            .GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ReferenceDataMapper.ToResponse(entity));
    }

    private static async Task<Results<Ok<IReadOnlyList<ReferenceDataResponse>>, NotFound>> GetChildrenAsync(
        string code,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string typeName = ResolveTypeName(httpContext);
        IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);

        DynamicReferenceDataEntity? parent = await reader
            .GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);

        if (parent is null)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<DynamicReferenceDataEntity> children = await reader
            .GetChildrenAsync(code, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ReferenceDataResponse> mapped = children
            .Select(ReferenceDataMapper.ToResponse).ToList();

        return TypedResults.Ok(mapped);
    }

    private static async Task<Created> CreateAsync(
        ReferenceDataCreateRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string typeName = ResolveTypeName(httpContext);
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

        await writer.CreateAsync(entity, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"{request.Code}");
    }

    private static async Task<Results<Ok, NotFound>> UpdateAsync(
        string code,
        ReferenceDataUpdateRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string typeName = ResolveTypeName(httpContext);
        IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);
        IReferenceDataStoreWriter<DynamicReferenceDataEntity> writer =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreWriter<DynamicReferenceDataEntity>>(typeName);

        DynamicReferenceDataEntity? existing = await reader
            .GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);

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
        existing.ParentCode = request.ParentCode;

        if (request.ExtraProperties is not null)
        {
            foreach ((string key, string value) in request.ExtraProperties)
            {
                existing.SetExtraProperty(key, value);
            }
        }

        await writer.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok();
    }

    private static async Task<Results<NoContent, NotFound>> DeactivateAsync(
        string code,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string typeName = ResolveTypeName(httpContext);
        IReferenceDataStoreReader<DynamicReferenceDataEntity> reader =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(typeName);
        IReferenceDataStoreWriter<DynamicReferenceDataEntity> writer =
            httpContext.RequestServices.GetRequiredKeyedService<IReferenceDataStoreWriter<DynamicReferenceDataEntity>>(typeName);

        DynamicReferenceDataEntity? existing = await reader
            .GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        await writer.SetActiveAsync(code, false, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}

/// <summary>
/// Endpoint metadata that stores the reference data type name for keyed service resolution.
/// </summary>
/// <param name="TypeName">The logical type name (e.g., <c>"Countries"</c>).</param>
internal sealed record ReferenceDataTypeNameMetadata(string TypeName);
