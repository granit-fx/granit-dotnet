using Granit.QueryEngine;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.Meta;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
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
/// GET endpoints for querying reference data entries.
/// All responses use <see cref="ReferenceDataResponse"/> — EF entities are never exposed directly.
/// </summary>
internal static class ReferenceDataReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints<TEntity>(
        this RouteGroupBuilder group,
        bool includeMetaEndpoint)
        where TEntity : ReferenceDataEntity
    {
        string entityName = typeof(TEntity).Name;

        // GET / — QueryEngine-powered paginated list with filtering, sorting, search
        group.MapGet("/", GetAllAsync<TEntity>)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
            .WithName($"GetAll{entityName}")
            .WithSummary($"Returns a filtered, sorted, and paginated list of {entityName} entries.")
            .WithDescription($"Lists {entityName} reference data entries using the Granit query engine. Accepts filter expressions, sort directives, pagination, and free-text search via query parameters. Returns a PagedResult of ReferenceDataResponse.")
            .Produces<PagedResult<ReferenceDataResponse>>();

        // GET /meta — query metadata (columns, filters, sorts)
        if (includeMetaEndpoint)
        {
            group.MapGet("/meta", GetMetaAsync<TEntity>)
                .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
                .WithName($"Get{entityName}Meta")
                .WithSummary($"Returns query metadata for {entityName} (columns, filters, sorts, presets).")
                .WithDescription($"Returns the query definition metadata for {entityName}: available columns with display labels and data types, supported filter operators, default sort order, and the current user's saved views. Use this to dynamically build query UIs without hardcoding column definitions.")
                .Produces<QueryMetadata>();
        }

        // GET /{code} — single entry by code
        group.MapGet("/{code}", GetByCodeAsync<TEntity>)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
            .WithName($"Get{entityName}ByCode")
            .WithSummary($"Returns a single {entityName} entry by code.")
            .WithDescription($"Returns the full {entityName} entry identified by its unique code, including all localized labels and validity dates. Returns 404 if no entry matches the code.")
            .Produces<ReferenceDataResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /{code}/children — hierarchy
        group.MapGet("/{code}/children", GetChildrenAsync<TEntity>)
            .RequireAuthorization(ReferenceDataPermissions.Entries.Read)
            .WithName($"Get{entityName}Children")
            .WithSummary($"Returns direct children of a {entityName} entry.")
            .WithDescription($"Returns all active direct children of the {entityName} entry identified by its code, ordered by sort order then code. For hierarchical reference data types that use ParentCode. Returns 404 if the parent entry does not exist.")
            .Produces<IReadOnlyList<ReferenceDataResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<PagedResult<ReferenceDataResponse>>> GetAllAsync<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine,
        [FromServices] IQueryableSource<TEntity> source,
        BindableQueryRequest request,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity
    {
        PagedResult<TEntity> result = await engine
            .ExecuteAsync(source.GetQueryable(), request.Value, cancellationToken)
            .ConfigureAwait(false);

        PagedResult<ReferenceDataResponse> mapped = new(
            result.Items.Select(ReferenceDataMapper.ToResponse).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(mapped);
    }

    private static async Task<Ok<QueryMetadata>> GetMetaAsync<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine,
        [FromServices] ISavedViewStoreReader savedViewStore,
        [FromServices] QueryDefinition<TEntity> definition,
        [FromServices] Granit.MultiTenancy.ICurrentTenant tenant,
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken)
        where TEntity : ReferenceDataEntity
    {
        string userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? string.Empty;

        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<SavedViewSummary> savedViews;
        try
        {
            IReadOnlyList<SavedView> views = await savedViewStore
                .GetListAsync(definition.Name, userId, tenantId, cancellationToken)
                .ConfigureAwait(false);

            savedViews = views.Select(v => new SavedViewSummary(
                v.Id, v.Name, v.IsShared, v.IsDefault)).ToList();
        }
        catch (NotImplementedException)
        {
            savedViews = [];
        }

        QueryMetadata metadata = engine.GetMetadata(savedViews);
        return TypedResults.Ok(metadata);
    }

    private static async Task<Results<Ok<ReferenceDataResponse>, ProblemHttpResult>> GetByCodeAsync<TEntity>(
        string code,
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity
    {
        TEntity? entity = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ReferenceDataMapper.ToResponse(entity));
    }

    private static async Task<Results<Ok<IReadOnlyList<ReferenceDataResponse>>, ProblemHttpResult>> GetChildrenAsync<TEntity>(
        string code,
        [FromServices] IReferenceDataStoreReader<TEntity> storeReader,
        CancellationToken cancellationToken = default)
        where TEntity : ReferenceDataEntity
    {
        TEntity? parent = await storeReader.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (parent is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        IReadOnlyList<TEntity> children = await storeReader
            .GetChildrenAsync(code, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ReferenceDataResponse> mapped = children
            .Select(ReferenceDataMapper.ToResponse).ToList();

        return TypedResults.Ok(mapped);
    }
}
