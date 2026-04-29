using System.Reflection;
using Granit.Authorization;
using Granit.Http.ODataExposure.Internal;
using Granit.Http.ODataExposure.Options;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;

namespace Granit.Http.ODataExposure.Extensions;

/// <summary>
/// Wires registered <c>QueryDefinition&lt;TEntity&gt;</c> instances onto OData
/// v4 EntitySets so external BI tools (Power BI / Excel / Tableau) can query
/// the application via the standard "Get Data → OData feed" connector.
/// </summary>
public static class ODataExposureEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the configured OData EntitySets under <paramref name="prefix"/>.
    /// Generates the EDM model at call time, exposes <c>$metadata</c> +
    /// <c>$service-document</c>, and one <c>GET /{EntitySetName}</c> per
    /// registered set. Each set's request flow is:
    /// <list type="number">
    ///   <item>Authentication — required by the surrounding pipeline (the framework's bearer token / DPoP).</item>
    ///   <item>Permission gate — when the set declared one via <see cref="ODataEntitySetBuilder{TEntity}.RequirePermission"/>.</item>
    ///   <item>Resolve <c>IQueryableSource&lt;TEntity&gt;</c> — emits a queryable already filtered by tenant + soft-delete (via <c>ApplyGranitConventions</c> on the host's DbContext).</item>
    ///   <item>Apply the <c>QueryDefinition</c> filter pipeline via <c>IQueryEngine.BuildFilteredQuery</c> with an empty <c>QueryRequest</c> — composes the framework's required filters.</item>
    ///   <item>Layer the user's <c>$filter</c> / <c>$select</c> / <c>$top</c> / <c>$skip</c> / <c>$orderby</c> via <c>ODataQueryOptions&lt;TEntity&gt;.ApplyTo</c> — user filters compose ON TOP of the framework filters, never bypassing them.</item>
    /// </list>
    /// </summary>
    /// <param name="endpoints">Endpoint route builder (the host's <c>app</c>).</param>
    /// <param name="prefix">Route prefix mounted at, conventionally <c>"/api/granit/odata"</c>.</param>
    /// <param name="configure">Configuration callback declaring EntitySets via <see cref="ODataExposureOptions"/>.</param>
    /// <returns>The outer <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <exception cref="ArgumentException">No EntitySet was declared in <paramref name="configure"/>.</exception>
    public static RouteGroupBuilder MapGranitODataEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix,
        Action<ODataExposureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(configure);

        ODataExposureOptions options = new();
        configure(options);

        if (options.Descriptors.Count == 0)
        {
            throw new ArgumentException(
                "MapGranitODataEndpoints requires at least one EntitySet — call options.EntitySet<TEntity, TQueryDefinition>(name) inside the configure callback.",
                nameof(configure));
        }

        IEdmModel edmModel = ODataEdmModelBuilder.Build(options.Descriptors);

        RouteGroupBuilder root = endpoints.MapGroup(prefix).WithTags("OData");

        // Service document + $metadata. Power BI Desktop reads $metadata to
        // populate its "Navigator" picker; service-document is the human-
        // readable index of EntitySet names.
        root.MapODataServiceDocument("", edmModel)
            .WithName("ODataServiceDocument")
            .WithSummary("OData v4 service document — lists every exposed EntitySet with its metadata link.")
            .WithDescription("Power BI / Excel / Tableau read this document to discover which EntitySets are available. Each set is queryable via the standard $filter / $select / $top / $skip / $orderby clauses.");

        root.MapODataMetadata("$metadata", edmModel)
            .WithName("ODataMetadata")
            .WithSummary("OData v4 EDM metadata document (CSDL XML).")
            .WithDescription("BI tools consume this CSDL document to populate their Navigator / table picker. The EDM is built from the application's registered QueryDefinition&lt;T&gt; instances.");

        // Per-set GET route. Closed-generic Activator dance avoids a
        // closed-generic registration per descriptor — same pattern the
        // analytics widget runners use for IQueryEngine resolution.
        foreach (ODataEntitySetDescriptor descriptor in options.Descriptors)
        {
            MethodInfo mapper = typeof(ODataExposureEndpointRouteBuilderExtensions)
                .GetMethod(nameof(MapEntitySetRoute), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(descriptor.EntityType);

            mapper.Invoke(null, [root, descriptor, edmModel]);
        }

        return root;
    }

    /// <summary>
    /// Wires one EntitySet's <c>GET /{EntitySetName}</c> route. Closed over
    /// <typeparamref name="TEntity"/> via reflection from the dispatcher so
    /// the runtime call stays generic-typed and compatible with
    /// <c>ODataQueryOptions&lt;TEntity&gt;</c> minimal-API parameter binding.
    /// </summary>
    private static void MapEntitySetRoute<TEntity>(
        RouteGroupBuilder root,
        ODataEntitySetDescriptor descriptor,
        IEdmModel edmModel)
        where TEntity : class
    {
        RouteHandlerBuilder route = root.MapGet(descriptor.EntitySetName, async (
                ODataQueryOptions<TEntity> options,
                [FromServices] IQueryableSource<TEntity> source,
                [FromServices] IQueryEngine<TEntity> engine,
                [FromServices] IPermissionChecker permissionChecker,
                CancellationToken cancellationToken) =>
            {
                if (descriptor.RequiredPermission is { } perm
                    && !await permissionChecker.IsGrantedAsync(perm, cancellationToken).ConfigureAwait(false))
                {
                    return (IResult)TypedResults.Forbid();
                }

                // Step 1: tenant + soft-delete already applied on `source` (the
                // host's IQueryableSource pulls from a DbContext that wired
                // ApplyGranitConventions). Step 2: layer the QueryDefinition's
                // filter pipeline. Step 3: apply OData query options on top.
                IQueryable<TEntity> filtered = engine.BuildFilteredQuery(
                    source.GetQueryable(), new QueryRequest());

                IQueryable applied = options.ApplyTo(filtered);
                return TypedResults.Ok(applied);
            })
            .WithODataModel(edmModel)
            .WithODataResult();

        route.WithName($"OData{descriptor.EntitySetName}List")
             .WithSummary($"Returns the {descriptor.EntitySetName} EntitySet, filtered by the framework's tenant + soft-delete pipeline.")
             .WithDescription($"OData v4 endpoint for the {descriptor.EntitySetName} set. Supports $filter, $select, $top, $skip, $orderby. Tenant and soft-delete filters are applied BEFORE any user $filter — the OData query never bypasses framework access control.")
             .Produces(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden);

        if (descriptor.RequiredPermission is not null)
        {
            // Authorization is enforced inline (permission checker) — keep
            // the metadata in sync so OpenAPI schemas can hint at the gate
            // even if the runtime check is the source of truth.
            route.RequireAuthorization();
        }
    }
}
