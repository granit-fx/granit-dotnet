using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.ReferenceData.Endpoints.Endpoints;

/// <summary>
/// GET endpoints for querying reference data entries.
/// All responses use <see cref="ReferenceDataResponse"/> — EF entities are never exposed directly.
/// </summary>
internal static class ReferenceDataReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints<TEntity>(
        this RouteGroupBuilder group)
        where TEntity : ReferenceDataEntity
    {
        group.MapGet("/", GetAllAsync<TEntity>)
            .WithName($"GetAll{typeof(TEntity).Name}")
            .WithSummary($"Returns a filtered, paginated list of {typeof(TEntity).Name} entries.")
            .WithDescription($"Lists {typeof(TEntity).Name} reference data entries with support for filtering (active-only, search term), sorting, and pagination. Labels are available in all 14 supported languages. By default, only active entries are returned.")
            .Produces<PagedResult<ReferenceDataResponse>>();

        group.MapGet("/{code}", GetByCodeAsync<TEntity>)
            .WithName($"Get{typeof(TEntity).Name}ByCode")
            .WithSummary($"Returns a single {typeof(TEntity).Name} entry by code.")
            .WithDescription($"Returns the full {typeof(TEntity).Name} entry identified by its unique code, including all localized labels and validity dates. Returns 404 if no entry matches the code.")
            .Produces<ReferenceDataResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{code}/children", GetChildrenAsync<TEntity>)
            .WithName($"Get{typeof(TEntity).Name}Children")
            .WithSummary($"Returns direct children of a {typeof(TEntity).Name} entry.")
            .WithDescription($"Returns all active direct children of the {typeof(TEntity).Name} entry identified by its code, ordered by sort order then code. For hierarchical reference data types that use ParentCode. Returns 404 if the parent entry does not exist.")
            .Produces<IReadOnlyList<ReferenceDataResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<PagedResult<ReferenceDataResponse>>> GetAllAsync<TEntity>(
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        [AsParameters] ReferenceDataQueryParameters parameters,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity
    {
        ReferenceDataQuery query = new(
            ActiveOnly: parameters.ActiveOnly,
            SearchTerm: parameters.Search,
            SortBy: parameters.SortBy,
            Descending: parameters.Descending,
            Page: parameters.Page,
            PageSize: parameters.PageSize);

        PagedResult<TEntity> result = await storeReader.GetAllAsync(query, cancellationToken).ConfigureAwait(false);

        PagedResult<ReferenceDataResponse> mapped = new(
            result.Items.Select(ReferenceDataMapper.ToResponse).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(mapped);
    }

    private static async Task<Results<Ok<ReferenceDataResponse>, NotFound>> GetByCodeAsync<TEntity>(
        string code,
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity
    {
        TEntity? entity = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ReferenceDataMapper.ToResponse(entity));
    }

    private static async Task<Results<Ok<IReadOnlyList<ReferenceDataResponse>>, NotFound>> GetChildrenAsync<TEntity>(
        string code,
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity
    {
        TEntity? parent = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (parent is null)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<TEntity> children = await storeReader
            .GetChildrenAsync(code, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ReferenceDataResponse> mapped = children
            .Select(ReferenceDataMapper.ToResponse).ToList();

        return TypedResults.Ok(mapped);
    }
}
