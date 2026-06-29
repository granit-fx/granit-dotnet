using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Granit.Entities;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Internal;
using Granit.QueryEngine.AspNetCore.Options;
using Granit.QueryEngine.Meta;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

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
    ///   <item><c>GET /</c> — paginated or grouped query (skipped when <see cref="QueryEndpointOptions.IncludeListEndpoint"/> is <c>false</c>)</item>
    ///   <item><c>GET /meta</c> — query metadata (skipped when <see cref="QueryEndpointOptions.IncludeMetaEndpoint"/> is <c>false</c>)</item>
    /// </list>
    /// <para>
    /// Saved-view persistence has moved to <c>Granit.Entities.Views.Endpoints</c> /
    /// <c>Granit.Entities.Views</c> (see ADR-047). Mount <c>MapGranitEntityViewsEndpoints</c>
    /// to expose the EntityView surface (list / share / pin / set-default).
    /// </para>
    /// </remarks>
    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "Setup-time reflection over an internal helper to dispatch to a generic projection-typed overload; private accessor used precisely so consumers cannot bypass the public API.")]
    public static RouteGroupBuilder MapGranitQuery<TEntity>(
        this IEndpointRouteBuilder endpoints,
        Func<IServiceProvider, IQueryable<TEntity>> sourceProvider,
        string prefix = "",
        Action<QueryEndpointOptions>? configure = null)
        where TEntity : class
    {
        QueryEndpointOptions options = new();
        configure?.Invoke(options);

        string entityName = typeof(TEntity).Name;

        RouteGroupBuilder group = endpoints.MapGranitGroup(prefix);

        // Only apply an explicit tag override. When TagName is null, inherit the
        // parent group's tag — previously we forced typeof(TEntity).Name, which
        // replaced module tags like "Auditing" with entity class names like
        // "AuditEntry" and leaked internal type names into the OpenAPI UI.
        if (!string.IsNullOrEmpty(options.TagName))
        {
            group.WithTags(options.TagName);
        }

        if (options.AuthorizationPolicy is not null)
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }
        else if (!options.AllowAnonymous)
        {
            group.RequireAuthorization();
        }

        if (options.IncludeListEndpoint)
        {
            // Resolve the query definition once at setup time. When it declares a projection,
            // dispatch to a generic helper that wires GET / to the typed ExecuteAsync<TDto>
            // overload. Reflection is used exclusively here (setup) — never per request.
            QueryDefinition<TEntity>? definitionForProjection = endpoints.ServiceProvider
                .GetService<QueryDefinition<TEntity>>();

            Type? projectionType = definitionForProjection?.GetProjectionType();
            LambdaExpression? projectionExpression = definitionForProjection?.GetProjectionExpression();

            IEndpointConventionBuilder listBuilder;
            if (projectionType is not null && projectionExpression is not null)
            {
                MethodInfo dispatch = typeof(QueryEndpointRouteBuilderExtensions)
                    .GetMethod(nameof(MapProjectedGetEndpoint), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(typeof(TEntity), projectionType);

                listBuilder = (IEndpointConventionBuilder)dispatch.Invoke(
                    null, [group, sourceProvider, projectionExpression, entityName])!;
            }
            else
            {
                listBuilder = MapNonProjectedGetEndpoint<TEntity>(group, sourceProvider, entityName);
            }

            // Tag the endpoint so entity-discovery surfaces (Granit.Entities.Endpoints)
            // can read RoutePattern.RawText off EndpointDataSource and expose the real
            // route on EntityDiscoveryLinks.List — same mechanism ASP.NET's own OpenAPI
            // generator uses for operation/tag/produces lookups.
            listBuilder.WithMetadata(new EntityEndpointMetadata(typeof(TEntity), EntityEndpointKind.List));
        }

        // GET /meta — query metadata
        if (options.IncludeMetaEndpoint)
        {
            group.MapGet("/meta", (
                [FromServices] IQueryEngine<TEntity> engine) =>
                QueryEndpointHandler.GetMetadata(engine))
            .WithName($"Get{entityName}Meta")
            .WithSummary($"Returns query metadata for {entityName} (columns, filters, sorts, presets).")
            .WithDescription($"Returns the query definition metadata for {entityName}: available columns with display labels and data types, supported filter operators, default sort order, presets, and quick filters. Use this to dynamically build query UIs without hardcoding column definitions.")
            .Produces<QueryMetadata>();
        }

        return group;
    }

    private static RouteHandlerBuilder MapNonProjectedGetEndpoint<TEntity>(
        RouteGroupBuilder group,
        Func<IServiceProvider, IQueryable<TEntity>> sourceProvider,
        string entityName)
        where TEntity : class
    {
        // Lambda returns different typed results (Ok<GroupedResult<T>> / Ok<PagedResult<T>>)
        // depending on the query mode — IResult is the only common type.
#pragma warning disable GRAPI001 // Results.Ok is needed here for polymorphic return
        return group.MapGet("/", async (
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
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .AddOpenApiOperationTransformer((op, ctx, ct) =>
            DescribeQueryEndpointAsync(op, ctx, typeof(PagedResult<TEntity>), typeof(GroupedResult<TEntity>), ct));
    }

    private static RouteHandlerBuilder MapProjectedGetEndpoint<TEntity, TDto>(
        RouteGroupBuilder group,
        Func<IServiceProvider, IQueryable<TEntity>> sourceProvider,
        LambdaExpression projectionLambda,
        string entityName)
        where TEntity : class
    {
        var projection = (Expression<Func<TEntity, TDto>>)projectionLambda;
        string dtoName = typeof(TDto).Name;

        // The projection applies to BOTH branches: paged uses SQL-level projection (typed
        // ExecuteAsync<TDto>); grouped applies the same projection in-memory after entity
        // materialization so GroupedResult.Items[] surfaces TDto, never the raw entity.
#pragma warning disable GRAPI001 // Results.Ok is needed here for polymorphic return
        return group.MapGet("/", async (
            [FromServices] IQueryEngine<TEntity> engine,
            BindableQueryRequest request,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IQueryable<TEntity> source = sourceProvider(httpContext.RequestServices);

            if (!string.IsNullOrWhiteSpace(request.Value.GroupBy))
            {
                GroupedResult<TDto> grouped = await engine
                    .ExecuteGroupedAsync(source, request.Value, projection, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(grouped);
            }

            PagedResult<TDto> paged = await engine
                .ExecuteAsync(source, request.Value, projection, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(paged);
        })
#pragma warning restore GRAPI001
        .WithName($"Query{entityName}")
        .WithSummary($"Returns a paged, filterable list of {dtoName} from {entityName}.")
        .WithDescription($"Executes a dynamic query against {entityName} using the Granit query engine and projects each row to {dtoName}. Accepts filter expressions, sort directives, column selection, pagination, and free-text search via query parameters. Returns a PagedResult<{dtoName}> by default. When the groupBy query parameter is specified, returns a GroupedResult<{dtoName}> with the same projection applied to the items inside each group.")
        .Produces<PagedResult<TDto>>()
        .Produces<GroupedResult<TDto>>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .AddOpenApiOperationTransformer((op, ctx, ct) =>
            DescribeQueryEndpointAsync(op, ctx, typeof(PagedResult<TDto>), typeof(GroupedResult<TDto>), ct));
    }

    /// <summary>
    /// Enriches a query endpoint operation with the standard QueryEngine query parameters
    /// (bound transparently by <see cref="BindableQueryRequest.BindAsync"/> so they are
    /// invisible to ASP.NET Core's default OpenAPI generator) and rewrites the 200 response
    /// as a <c>oneOf</c> union of the paged and grouped result shapes.
    /// </summary>
    private static async Task DescribeQueryEndpointAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        Type pagedResultType,
        Type groupedResultType,
        CancellationToken cancellationToken)
    {
        operation.Parameters ??= [];

        AddParam(operation, "page", "1-based page index. Ignored when cursor is supplied.",
            new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32", Minimum = "1" });
        AddParam(operation, "pageSize", "Number of items per page (1-500). Server-side cap applies.",
            new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32", Minimum = "1", Maximum = "500" });
        AddParam(operation, "cursor", "Opaque cursor for keyset pagination. When supplied, overrides page.",
            new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null });
        AddParam(operation, "search", "Free-text search across searchable columns declared in the query definition.",
            new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null });
        AddParam(operation, "sort", "Comma-separated sort directives. Prefix a field with '-' for descending (e.g. '-createdAt,lastName').",
            new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null });
        AddParam(operation, "groupBy", "Field to group results by. When set, the response shape becomes GroupedResult instead of PagedResult.",
            new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null });
        AddParam(operation, "quickFilters", "Comma-separated quick-filter names (declared in the query definition).",
            new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null });
        AddParam(operation, "skipTotalCount", "Skip the total row count for faster pagination when the client doesn't need it.",
            new OpenApiSchema { Type = JsonSchemaType.Boolean | JsonSchemaType.Null });
        AddParam(operation, "filter", "Bracketed filter expressions: 'filter[field.op]=value'. Example: 'filter[name.contains]=alice&filter[age.gt]=18'.",
            new OpenApiSchema
            {
                Type = JsonSchemaType.Object | JsonSchemaType.Null,
                AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
            },
            style: ParameterStyle.DeepObject,
            explode: true);
        AddParam(operation, "presets", "Bracketed preset selectors: 'presets[group]=name'. Applies preset filter groups declared in the query definition.",
            new OpenApiSchema
            {
                Type = JsonSchemaType.Object | JsonSchemaType.Null,
                AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
            },
            style: ParameterStyle.DeepObject,
            explode: true);

        if (operation.Responses is not null
            && operation.Responses.TryGetValue("200", out IOpenApiResponse? response)
            && response is OpenApiResponse concrete
            && concrete.Content is not null
            && concrete.Content.TryGetValue("application/json", out OpenApiMediaType? media))
        {
            // GetOrCreateSchemaAsync returns the concrete schema but does NOT reliably
            // persist it in components.schemas when the same response status has two
            // .Produces<>() entries — ASP.NET Core dedups at status code, so only the
            // second (GroupedResult) reaches the schema collector. We register both
            // explicitly so OneOf $refs resolve and codegen tools don't break.
            IOpenApiSchema pagedSchema = await context
                .GetOrCreateSchemaAsync(pagedResultType, null, cancellationToken)
                .ConfigureAwait(false);
            IOpenApiSchema groupedSchema = await context
                .GetOrCreateSchemaAsync(groupedResultType, null, cancellationToken)
                .ConfigureAwait(false);

            EnsureRegisteredInComponents(context.Document, pagedResultType, pagedSchema);
            EnsureRegisteredInComponents(context.Document, groupedResultType, groupedSchema);

            media.Schema = new OpenApiSchema
            {
                OneOf =
                [
                    new OpenApiSchemaReference(GetSchemaReferenceId(pagedResultType), null),
                    new OpenApiSchemaReference(GetSchemaReferenceId(groupedResultType), null),
                ],
                Description = "PagedResult when groupBy is absent; GroupedResult otherwise.",
            };
        }
    }

    private static void EnsureRegisteredInComponents(
        OpenApiDocument? document,
        Type type,
        IOpenApiSchema schema)
    {
        if (document is null || schema is OpenApiSchemaReference)
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        string id = GetSchemaReferenceId(type);
        document.Components.Schemas.TryAdd(id, schema);
    }

    /// <summary>
    /// Mirrors the default schema reference id convention used by ASP.NET Core's OpenAPI
    /// generator for closed generic types: <c>{TypeName}Of{Arg1}And{Arg2}...</c>.
    /// E.g. <c>PagedResult&lt;AuditEntryResponse&gt;</c> → <c>PagedResultOfAuditEntryResponse</c>.
    /// </summary>
    private static string GetSchemaReferenceId(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string name = type.Name[..type.Name.IndexOf('`')];
        string args = string.Join("And", type.GetGenericArguments().Select(GetSchemaReferenceId));
        return $"{name}Of{args}";
    }

    private static void AddParam(
        OpenApiOperation operation,
        string name,
        string description,
        OpenApiSchema schema,
        ParameterStyle? style = null,
        bool? explode = null)
    {
        IList<IOpenApiParameter> parameters = operation.Parameters ??= [];

        if (parameters.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal) && p.In == ParameterLocation.Query))
        {
            return;
        }

        OpenApiParameter parameter = new()
        {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            Description = description,
            Schema = schema,
        };

        if (style is not null)
        {
            parameter.Style = style;
        }

        if (explode is not null)
        {
            parameter.Explode = (bool)explode;
        }

        parameters.Add(parameter);
    }
}
