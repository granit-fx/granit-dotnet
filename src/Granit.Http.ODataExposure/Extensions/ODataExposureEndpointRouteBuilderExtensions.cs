using System.Reflection;
using Granit.Authorization;
using Granit.Http.ODataExposure.Diagnostics;
using Granit.Http.ODataExposure.Internal;
using Granit.Http.ODataExposure.Options;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.RateLimiting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.OData.Edm;

namespace Granit.Http.ODataExposure.Extensions;

/// <summary>
/// Wires registered <c>QueryDefinition&lt;TEntity&gt;</c> instances onto OData
/// v4 EntitySets so external BI tools (Power BI / Excel / Tableau) can query
/// the application via the standard "Get Data → OData feed" connector.
/// </summary>
public static class ODataExposureEndpointRouteBuilderExtensions
{
    /// <summary>Header set on the response when a user-supplied <c>$top</c> was clamped to the EntitySet's <see cref="ODataEntitySetDescriptor.MaxTop"/>. Lets observability tools spot misconfigured BI refresh jobs.</summary>
    internal const string MaxTopAppliedHeader = "OData-MaxTop-Applied";

    /// <summary>Rate-limit policy name applied to every OData route in the group. Hosts configure quotas under <c>RateLimiting:Policies:granit-odata</c>.</summary>
    public const string RateLimitPolicyName = "granit-odata";

    /// <summary>
    /// Maps the configured OData EntitySets under <paramref name="prefix"/>.
    /// Generates the EDM model at call time, exposes <c>$metadata</c> +
    /// <c>$service-document</c>, and one <c>GET /{EntitySetName}</c> per
    /// registered set. Each set's request flow is:
    /// <list type="number">
    ///   <item>Authentication — required by the surrounding pipeline (the framework's bearer token / DPoP).</item>
    ///   <item>Permission gate — when the set declared one via <see cref="ODataEntitySetBuilder{TEntity}.RequirePermission"/>.</item>
    ///   <item>C3 hardening (#1392) — <c>$count</c>, <c>$expand</c> whitelist enforcement against the per-set descriptor.</item>
    ///   <item>Resolve <c>IQueryableSource&lt;TEntity&gt;</c> — emits a queryable already filtered by tenant + soft-delete (via <c>ApplyGranitConventions</c> on the host's DbContext).</item>
    ///   <item>Apply the <c>QueryDefinition</c> filter pipeline via <c>IQueryEngine.BuildFilteredQuery</c> with an empty <c>QueryRequest</c> — composes the framework's required filters.</item>
    ///   <item>Layer the user's <c>$filter</c> / <c>$select</c> / <c>$top</c> / <c>$skip</c> / <c>$orderby</c> via <c>ODataQueryOptions&lt;TEntity&gt;.ApplyTo</c> with the per-set <c>PageSize</c> and <c>MaxTop</c> caps applied — user filters compose ON TOP of framework filters, never bypassing them.</item>
    /// </list>
    /// </summary>
    /// <param name="endpoints">Endpoint route builder (the host's <c>app</c>).</param>
    /// <param name="prefix">Route prefix mounted at, conventionally <c>"/api/{version}/odata"</c>.</param>
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

        ValidateStrictConfiguration(options.Descriptors);

        IEdmModel edmModel = ODataEdmModelBuilder.Build(options.Descriptors);

        RouteGroupBuilder root = endpoints.MapGroup(prefix)
            .WithTags("OData")
            .RequireGranitRateLimiting(RateLimitPolicyName);

        root.MapODataServiceDocument("", edmModel)
            .WithName("ODataServiceDocument")
            .WithSummary("OData v4 service document — lists every exposed EntitySet with its metadata link.")
            .WithDescription("Power BI / Excel / Tableau read this document to discover which EntitySets are available. Each set is queryable via the standard $filter / $select / $top / $skip / $orderby clauses.");

        root.MapODataMetadata("$metadata", edmModel)
            .WithName("ODataMetadata")
            .WithSummary("OData v4 EDM metadata document (CSDL XML).")
            .WithDescription("BI tools consume this CSDL document to populate their Navigator / table picker. The EDM is built from the application's registered QueryDefinition&lt;T&gt; instances.");

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
    /// Strict-config validator (C6 #1395). Refuses to start the host when a
    /// registered EntitySet hasn't acknowledged its security-sensitive
    /// configuration choices: permission gating and <c>$expand</c> policy.
    /// The framework deliberately does NOT default-deny and does NOT
    /// silently apply safe defaults — those would let convention drift
    /// reach production unchecked. Failing fast at <c>MapGranitODataEndpoints</c>
    /// time is the equivalent of an architecture test for a config surface
    /// that lives inside a closure (and is therefore not statically
    /// reflectable).
    /// </summary>
    /// <exception cref="InvalidOperationException">Any descriptor lacks both <see cref="ODataEntitySetBuilder{TEntity}.RequirePermission"/> and <see cref="ODataEntitySetBuilder{TEntity}.AllowAnonymousAccess"/>, OR neither <see cref="ODataEntitySetBuilder{TEntity}.ExpandWhitelist"/> nor <see cref="ODataEntitySetBuilder{TEntity}.DisableExpand"/>.</exception>
    private static void ValidateStrictConfiguration(IReadOnlyList<ODataEntitySetDescriptor> descriptors)
    {
        List<string> errors = [];

        foreach (ODataEntitySetDescriptor descriptor in descriptors)
        {
            if (descriptor.RequiredPermission is null && !descriptor.AnonymousAccessAcknowledged)
            {
                errors.Add(
                    $"EntitySet '{descriptor.EntitySetName}' must call either RequirePermission(string) or AllowAnonymousAccess() — implicit anonymous OData access is rejected by the strict-config validator (C6 #1395). Convention: {RequiredPermissionConvention(descriptor)}.");
            }

            if (!descriptor.ExpandConfigurationAcknowledged)
            {
                errors.Add(
                    $"EntitySet '{descriptor.EntitySetName}' must call either ExpandWhitelist(...) or DisableExpand() — implicit \"$expand disabled\" is rejected by the strict-config validator (C6 #1395). Use DisableExpand() to declare the intent, or ExpandWhitelist(\"NavProp1\", ...) to allow specific navigations.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "OData EntitySet configuration is incomplete:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
        }
    }

    /// <summary>Suggests the conventional <c>OData.{Module}.{Entity}.Read</c> permission name for the descriptor's entity, used in the strict-config error message.</summary>
    private static string RequiredPermissionConvention(ODataEntitySetDescriptor descriptor) =>
        $"OData.{descriptor.EntityType.Namespace?.Split('.').LastOrDefault() ?? "Module"}.{descriptor.EntityType.Name}.Read";

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
        RouteHandlerBuilder route = root.MapGet(descriptor.EntitySetName, async Task<object?> (
                ODataQueryOptions<TEntity> options,
                HttpContext httpContext,
                [FromServices] IQueryableSource<TEntity> source,
                [FromServices] IQueryEngine<TEntity> engine,
                [FromServices] IPermissionChecker permissionChecker,
                [FromServices] ODataExposureMetrics metrics,
                [FromServices] ICurrentTenant? currentTenant,
                CancellationToken cancellationToken) =>
            {
                if (descriptor.RequiredPermission is { } perm
                    && !await permissionChecker.IsGrantedAsync(perm, cancellationToken).ConfigureAwait(false))
                {
                    return TypedResults.Forbid();
                }

                string? tenantTag = currentTenant is { IsAvailable: true, Id: { } tid }
                    ? tid.ToString()
                    : null;

                if (RejectIfCountDisallowed(httpContext, descriptor) is { } countRejection)
                {
                    metrics.RecordRejectedQuery(descriptor.EntitySetName, "count_disabled", tenantTag);
                    return countRejection;
                }

                if (RejectIfExpandUnauthorised(httpContext, descriptor) is { } expandRejection)
                {
                    metrics.RecordRejectedQuery(descriptor.EntitySetName, "expand_not_whitelisted", tenantTag);
                    return expandRejection;
                }

                ApplyMaxTopAppliedHeader(httpContext, descriptor, metrics, tenantTag);

                IQueryable<TEntity> filtered = engine.BuildFilteredQuery(
                    source.GetQueryable(), new QueryRequest());

                ODataQuerySettings querySettings = new() { PageSize = descriptor.PageSize };
                IQueryable applied = options.ApplyTo(filtered, querySettings);

                // Return the raw IQueryable so the WithODataResult filter wraps it
                // in an ODataResult ({ "@odata.context": "...", "value": [...] }).
                // TypedResults.Ok would short-circuit the filter — its IResult-check
                // returns the inner Ok<IQueryable> unwrapped, which serializes as a
                // bare JSON array and breaks every BI-tool consumer expecting v4.
                return applied;
            })
            .WithODataModel(edmModel)
            .WithODataResult()
            .WithODataOptions(opts => opts.SetMaxTop(descriptor.MaxTop));

        route.WithName($"OData{descriptor.EntitySetName}List")
             .WithSummary($"Returns the {descriptor.EntitySetName} EntitySet, filtered by the framework's tenant + soft-delete pipeline.")
             .WithDescription($"OData v4 endpoint for the {descriptor.EntitySetName} set. Supports $filter, $select, $top, $skip, $orderby. Tenant and soft-delete filters are applied BEFORE any user $filter — the OData query never bypasses framework access control. Per-set caps: MaxTop={descriptor.MaxTop}, PageSize={descriptor.PageSize}, $count={(descriptor.CountEnabled ? "enabled" : "disabled")}, $expand={(descriptor.ExpandWhitelist is null or { Count: 0 } ? "disabled" : string.Join(",", descriptor.ExpandWhitelist))}.")
             .Produces(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden);

        if (descriptor.RequiredPermission is not null)
        {
            route.RequireAuthorization();
        }
    }

    /// <summary>
    /// Returns a <c>400</c> result when the request asks for <c>$count=true</c>
    /// on an EntitySet whose owner did NOT call <see cref="ODataEntitySetBuilder{TEntity}.EnableCount"/>.
    /// Default-disabled because <c>$count</c> on a 50M-row table forces a
    /// full-table scan per request — Power BI refresh jobs can hit it
    /// repeatedly.
    /// </summary>
    private static ProblemHttpResult? RejectIfCountDisallowed(HttpContext httpContext, ODataEntitySetDescriptor descriptor)
    {
        if (descriptor.CountEnabled)
        {
            return null;
        }

        if (httpContext.Request.Query.TryGetValue("$count", out StringValues raw)
            && string.Equals(raw.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: $"$count is not enabled on the '{descriptor.EntitySetName}' EntitySet. Contact the API owner to enable it.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Query option not allowed");
        }

        return null;
    }

    /// <summary>
    /// Validates the user's <c>$expand</c> clause against the EntitySet's
    /// whitelist. Default whitelist is empty (or <see langword="null"/>) →
    /// <c>$expand</c> rejected outright. Non-empty whitelist allows only the
    /// listed top-level navigation properties; nested-expand depth is
    /// enforced separately by <c>ODataMiniOptions.SetMaxTop</c> / the
    /// framework's <c>MaxExpansionDepth</c> validator hook.
    /// </summary>
    private static ProblemHttpResult? RejectIfExpandUnauthorised(HttpContext httpContext, ODataEntitySetDescriptor descriptor)
    {
        if (!httpContext.Request.Query.TryGetValue("$expand", out StringValues raw))
        {
            return null;
        }

        string rawExpand = raw.ToString();
        if (string.IsNullOrWhiteSpace(rawExpand))
        {
            return null;
        }

        IReadOnlyList<string>? whitelist = descriptor.ExpandWhitelist;
        if (whitelist is null || whitelist.Count == 0)
        {
            return TypedResults.Problem(
                detail: $"$expand is not enabled on the '{descriptor.EntitySetName}' EntitySet.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Query option not allowed");
        }

        // Top-level navigation extraction — `Customer($expand=Lines),Address`
        // → ["Customer", "Address"]. We only validate the first segment of
        // each comma-split entry; nested expansion semantics are validated
        // separately by MaxExpansionDepth.
        string[] requested = [..
            rawExpand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment =>
                {
                    int parenIndex = segment.IndexOf('(', StringComparison.Ordinal);
                    return parenIndex >= 0 ? segment[..parenIndex].Trim() : segment;
                })];

        foreach (string property in requested)
        {
            if (string.IsNullOrEmpty(property))
            {
                continue;
            }

            if (!whitelist.Contains(property, StringComparer.OrdinalIgnoreCase))
            {
                return TypedResults.Problem(
                    detail: $"$expand of property '{property}' is not permitted on the '{descriptor.EntitySetName}' EntitySet. Allowed: {string.Join(", ", whitelist)}.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Expand property not whitelisted");
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the <c>OData-MaxTop-Applied</c> response header when the user's
    /// <c>$top</c> exceeded the descriptor's cap — the framework clamps
    /// silently (per acceptance criteria), the header surfaces the clamping
    /// to observability tooling. Also bumps the rejected-query counter for
    /// the same reason.
    /// </summary>
    private static void ApplyMaxTopAppliedHeader(
        HttpContext httpContext,
        ODataEntitySetDescriptor descriptor,
        ODataExposureMetrics metrics,
        string? tenantTag)
    {
        if (!httpContext.Request.Query.TryGetValue("$top", out StringValues topRaw)
            || !int.TryParse(topRaw.ToString(), out int requestedTop)
            || requestedTop <= descriptor.MaxTop)
        {
            return;
        }

        httpContext.Response.Headers[MaxTopAppliedHeader] =
            descriptor.MaxTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
        metrics.RecordTopClamped(descriptor.EntitySetName, tenantTag);
    }
}
