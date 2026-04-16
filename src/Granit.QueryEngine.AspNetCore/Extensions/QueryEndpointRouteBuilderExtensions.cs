using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Internal;
using Granit.QueryEngine.AspNetCore.Options;
using Granit.QueryEngine.Meta;
using Granit.QueryEngine.SavedViews;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.QueryEngine.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering query endpoints on <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class QueryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps query endpoints for <typeparamref name="TEntity"/>, resolving the base
    /// <see cref="IQueryable{T}"/> from <see cref="IQueryableSource{TEntity}"/> in DI.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">Optional route prefix for the query group (e.g. <c>"products"</c>). Defaults to empty.</param>
    /// <param name="configure">Optional delegate to customize <see cref="QueryEndpointOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitQuery<TEntity>(
        this IEndpointRouteBuilder endpoints,
        string prefix = "",
        Action<QueryEndpointOptions>? configure = null)
        where TEntity : class
    {
        return endpoints.MapGranitQuery<TEntity>(
            sp => sp.GetRequiredService<IQueryableSource<TEntity>>().GetQueryable(),
            prefix,
            configure);
    }

    /// <summary>
    /// Maps query endpoints for <typeparamref name="TEntity"/> using the specified
    /// <paramref name="sourceProvider"/> to resolve the base <see cref="IQueryable{T}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="sourceProvider">
    /// Delegate that resolves the base <see cref="IQueryable{TEntity}"/> from the DI container.
    /// Example: <c>sp => sp.GetRequiredService&lt;AppDbContext&gt;().Products.AsNoTracking()</c>.
    /// </param>
    /// <param name="prefix">Optional route prefix for the query group (e.g. <c>"products"</c>). Defaults to empty (mounts on the current group).</param>
    /// <param name="configure">Optional delegate to customize <see cref="QueryEndpointOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>Registers the following endpoints:</para>
    /// <list type="bullet">
    ///   <item><c>GET /</c> — paginated or grouped query</item>
    ///   <item><c>GET /meta</c> — query metadata (columns, filters, sorts, etc.)</item>
    ///   <item><c>GET /saved-views</c> — list saved views</item>
    ///   <item><c>POST /saved-views</c> — create saved view</item>
    ///   <item><c>PUT /saved-views/{id}</c> — update saved view</item>
    ///   <item><c>DELETE /saved-views/{id}</c> — delete saved view</item>
    ///   <item><c>POST /saved-views/{id}/set-default</c> — set default saved view</item>
    /// </list>
    /// </remarks>
    public static RouteGroupBuilder MapGranitQuery<TEntity>(
        this IEndpointRouteBuilder endpoints,
        Func<IServiceProvider, IQueryable<TEntity>> sourceProvider,
        string prefix = "",
        Action<QueryEndpointOptions>? configure = null)
        where TEntity : class
    {
        QueryEndpointOptions options = new();
        configure?.Invoke(options);

        string tag = options.TagName ?? typeof(TEntity).Name;
        string entityName = typeof(TEntity).Name;

        RouteGroupBuilder group = endpoints.MapGranitGroup(prefix).WithTags(tag);

        if (options.AuthorizationPolicy is not null)
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }
        else if (!options.AllowAnonymous)
        {
            group.RequireAuthorization();
        }

        // GET / — paginated or grouped query
        // Lambda returns different typed results (Ok<GroupedResult<T>> / Ok<PagedResult<T>>)
        // depending on the query mode — IResult is the only common type.
#pragma warning disable GRAPI001 // Results.Ok is needed here for polymorphic return
        group.MapGet("/", async (
            [FromServices] IQueryEngine<TEntity> engine,
            BindableQueryRequest request,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IQueryable<TEntity> source = sourceProvider(httpContext.RequestServices);

            if (!string.IsNullOrWhiteSpace(request.Value.GroupBy))
            {
                GroupedResult<TEntity> grouped = await engine
                    .ExecuteGroupedAsync(source, request.Value, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(grouped);
            }

            PagedResult<TEntity> paged = await engine
                .ExecuteAsync(source, request.Value, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(paged);
        })
#pragma warning restore GRAPI001
        .WithName($"Query{entityName}")
        .WithSummary($"Returns a filtered, sorted, and paginated list of {entityName} entries.")
        .WithDescription($"Executes a dynamic query against {entityName} using the Granit query engine. Accepts filter expressions, sort directives, column selection, pagination, and free-text search via query parameters. Returns a PagedResult by default. When the groupBy query parameter is specified, returns a GroupedResult instead (same status code, different shape).")
        .Produces<PagedResult<TEntity>>()
        .Produces<GroupedResult<TEntity>>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        // GET /meta — query metadata
        if (options.IncludeMetaEndpoint)
        {
            group.MapGet("/meta", async (
                [FromServices] IQueryEngine<TEntity> engine,
                [FromServices] ISavedViewStoreReader savedViewStore,
                [FromServices] QueryDefinition<TEntity> definition,
                Granit.MultiTenancy.ICurrentTenant tenant,
                System.Security.Claims.ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                return await QueryEndpointHandler.GetMetadataAsync(
                    engine, savedViewStore, definition, tenant, user, cancellationToken)
                    .ConfigureAwait(false);
            })
            .WithName($"Get{entityName}Meta")
            .WithSummary($"Returns query metadata for {entityName} (columns, filters, sorts, presets).")
            .WithDescription($"Returns the query definition metadata for {entityName}: available columns with display labels and data types, supported filter operators, default sort order, and the current user's saved views. Use this to dynamically build query UIs without hardcoding column definitions.")
            .Produces<QueryMetadata>();
        }

        // Saved views CRUD
        if (options.IncludeSavedViewEndpoints)
        {
            QueryDefinition<TEntity>? definition = endpoints.ServiceProvider
                .GetService<QueryDefinition<TEntity>>();

            string entityType = definition?.Name ?? entityName;
            group.MapSavedViewEndpoints(entityType);
        }

        return group;
    }

}
