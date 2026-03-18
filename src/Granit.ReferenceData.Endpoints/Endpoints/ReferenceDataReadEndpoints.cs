using Granit.Querying;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.ReferenceData.Endpoints.Endpoints;

/// <summary>
/// GET endpoints for querying reference data entries.
/// </summary>
internal static class ReferenceDataReadEndpoints
{
    /// <summary>
    /// Registers GET / and GET /{code} onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapReadEndpoints<TEntity>(
        this RouteGroupBuilder group)
        where TEntity : ReferenceDataEntity
    {
        group.MapGet("/", GetAllAsync<TEntity>)
            .WithName($"GetAll{typeof(TEntity).Name}")
            .WithSummary($"Returns a filtered, paginated list of {typeof(TEntity).Name} entries.")
            .WithDescription($"Lists {typeof(TEntity).Name} reference data entries with support for filtering (active-only, search term), sorting, and pagination. Labels are available in all 14 supported languages. By default, only active entries are returned.");

        group.MapGet("/{code}", GetByCodeAsync<TEntity>)
            .WithName($"Get{typeof(TEntity).Name}ByCode")
            .WithSummary($"Returns a single {typeof(TEntity).Name} entry by code.")
            .WithDescription($"Returns the full {typeof(TEntity).Name} entry identified by its unique code, including all localized labels and validity dates. Returns 404 if no entry matches the code.");

        return group;
    }

    private static async Task<Ok<PagedResult<TEntity>>> GetAllAsync<TEntity>(
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

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<TEntity>, NotFound>> GetByCodeAsync<TEntity>(
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

        return TypedResults.Ok(entity);
    }
}
