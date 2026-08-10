using System.Collections.Frozen;
using System.Reflection;
using Granit.Authorization;
using Granit.DataExchange.Export;
using Granit.Domain;
using Granit.Entities;
using Granit.Http.ODataExposure.Diagnostics;
using Granit.Http.ODataExposure.Internal;
using Granit.Http.ODataExposure.Options;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Filtering.Exceptions;
using Granit.QueryEngine.Meta;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

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

    /// <summary>Problem-details title for every rejected OData query option.</summary>
    private const string QueryOptionNotSupportedTitle = "Query option not supported";

    /// <summary>Rate-limit policy name applied to every tenant-feed OData route. Hosts configure quotas under <c>RateLimiting:Policies:granit-odata</c>.</summary>
    public const string RateLimitPolicyName = "granit-odata";

    /// <summary>Rate-limit policy name applied to every host-feed OData route. Distinct policy so the wider quotas typical of host-side BI workflows do not bleed onto tenant-facing routes. Hosts configure quotas under <c>RateLimiting:Policies:granit-odata-host</c> (recommended <c>PartitionBy: User</c>).</summary>
    public const string HostRateLimitPolicyName = "granit-odata-host";

    /// <summary>
    /// Maps the configured OData EntitySets under <paramref name="prefix"/>.
    /// Generates the EDM model at call time, exposes <c>$metadata</c> +
    /// <c>$service-document</c> (behind the mount's explicit metadata
    /// authorization stance — see <see cref="ODataExposureOptions.RequireMetadataPermission"/> /
    /// <see cref="ODataExposureOptions.AllowAnonymousMetadata"/>), and one
    /// <c>GET /{EntitySetName}</c> per registered set. Each set's request flow is:
    /// <list type="number">
    ///   <item>Authentication — required by the surrounding pipeline (the framework's bearer token / DPoP).</item>
    ///   <item>Permission gate — when the set declared one via <see cref="ODataEntitySetBuilder{TEntity}.RequirePermission"/>.</item>
    ///   <item>C3 hardening (#1392, hardened by #3005) — <c>$count</c> gate, recursive <c>$expand</c> whitelist + depth enforcement over the parsed AST.</item>
    ///   <item>Resolve <c>IQueryableSource&lt;TEntity&gt;</c> — emits a queryable already filtered by tenant + soft-delete (via <c>ApplyGranitConventions</c> on the host's DbContext).</item>
    ///   <item>Translate the user's <c>$filter</c> AST into the engine's strict <c>QueryPredicate</c> tree (#3004) — untranslatable constructs return <c>400</c> with an actionable detail.</item>
    ///   <item>Apply the <c>QueryDefinition</c> filter pipeline via <c>IQueryEngine.BuildFilteredQuery(source, request, predicate)</c> — the framework's required filters AND the user's <c>$filter</c> are enforced by the engine's single enforcement point (filterable-column whitelist, operator inference, structural guards). Violations return <c>400</c> listing every offending field.</item>
    ///   <item>Validate the remaining query options against the per-route <c>ODataValidationSettings</c> (allowed options, <c>$orderby</c> whitelist derived from the definition's sortable columns, node-count caps, <c>MaxExpansionDepth</c>).</item>
    ///   <item>Layer <c>$select</c> / <c>$expand</c> / <c>$top</c> / <c>$skip</c> / <c>$orderby</c> via <c>ODataQueryOptions&lt;TEntity&gt;.ApplyTo</c> with the per-set <c>PageSize</c> and <c>MaxTop</c> caps applied. <c>$filter</c> is passed as the ignore flag so it is never applied a second time.</item>
    /// </list>
    /// </summary>
    /// <param name="endpoints">Endpoint route builder (the host's <c>app</c>).</param>
    /// <param name="prefix">Route prefix mounted at, conventionally <c>"/api/{version}/odata"</c>.</param>
    /// <param name="configure">Configuration callback declaring EntitySets via <see cref="ODataExposureOptions"/>.</param>
    /// <returns>The outer <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <exception cref="ArgumentException">No EntitySet was declared in <paramref name="configure"/>.</exception>
    /// <exception cref="InvalidOperationException">Strict-config validation failed (missing permission or $expand intent, missing $metadata stance, invalid $expand path, expand target without ExportDefinition, ADR-050 gates).</exception>
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

        return MapEndpointsCore(
            endpoints,
            prefix,
            options.Descriptors,
            ODataFeedKind.Tenant,
            RateLimitPolicyName,
            ODataEdmModelBuilder.TenantContainerName,
            options.MetadataPermission,
            options.MetadataStanceDeclared);
    }

    /// <summary>
    /// Maps the configured <b>host-feed</b> OData EntitySets under
    /// <paramref name="prefix"/> — the cross-tenant feed reserved for host
    /// operators (finance ops, compliance, capacity planning).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four strict-config gates apply on top of the tenant-feed validator:
    /// </para>
    /// <list type="number">
    ///   <item>The required permission MUST resolve to a <c>PermissionDefinition</c> with <c>MultiTenancySides == Host</c>; <c>Tenant</c> and <c>Both</c> are rejected at startup.</item>
    ///   <item>Anonymous access is not allowed (the host builder does not expose <c>AllowAnonymousAccess</c>).</item>
    ///   <item>Every host-feed EntitySet whose entity implements <c>IMultiTenant</c> MUST have called <c>AcknowledgeCrossTenantExposure(q =&gt; q.IgnoreQueryFilters([GranitFilterNames.MultiTenant]))</c>. The bypass lambda is supplied by the host so this module stays free of an EF Core dependency, and so the explicit "I know what I'm doing" lives in code, not in a flag.</item>
    ///   <item><see cref="ODataHostExposureOptions.RequireMetadataPermission"/> is REQUIRED (#3005) — the host-feed <c>$metadata</c> / service document have no anonymous variant, and the permission must be <c>MultiTenancySides.Host</c> like the entity permissions.</item>
    /// </list>
    /// <para>
    /// Container name for the EDM is <c>HostContainer</c> (vs <c>Container</c>
    /// on the tenant-feed) so any BI client that mistakenly reuses the wrong
    /// <c>$metadata</c> document gets an immediate schema mismatch.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">Endpoint route builder (the host's <c>app</c>).</param>
    /// <param name="prefix">Route prefix mounted at, conventionally <c>"/api/{version}/odata/host"</c>.</param>
    /// <param name="configure">Configuration callback declaring host-feed EntitySets via <see cref="ODataHostExposureOptions"/>.</param>
    /// <returns>The outer <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <exception cref="ArgumentException">No EntitySet was declared in <paramref name="configure"/>.</exception>
    /// <exception cref="InvalidOperationException">Strict-config validation failed (missing permission, wrong <c>MultiTenancySides</c>, missing <c>AcknowledgeCrossTenantExposure</c>, missing <c>$expand</c> intent, missing metadata permission, or unresolvable permission definition).</exception>
    public static RouteGroupBuilder MapGranitODataHostEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix,
        Action<ODataHostExposureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(configure);

        ODataHostExposureOptions options = new();
        configure(options);

        if (options.Descriptors.Count == 0)
        {
            throw new ArgumentException(
                "MapGranitODataHostEndpoints requires at least one EntitySet — call options.EntitySet<TEntity, TQueryDefinition>(name) inside the configure callback.",
                nameof(configure));
        }

        return MapEndpointsCore(
            endpoints,
            prefix,
            options.Descriptors,
            ODataFeedKind.Host,
            HostRateLimitPolicyName,
            ODataEdmModelBuilder.HostContainerName,
            options.MetadataPermission,
            metadataStanceDeclared: options.MetadataPermission is not null);
    }

    /// <summary>
    /// Shared route-mapping pipeline used by both <see cref="MapGranitODataEndpoints"/>
    /// and <see cref="MapGranitODataHostEndpoints"/>. Validates strict-config
    /// (feed-kind aware, including the mount's $metadata stance), resolves the
    /// per-type EDM whitelists from the registered <c>ExportDefinition</c>s
    /// (per ADR-050, including every navigation-target type reachable through
    /// a whitelisted <c>$expand</c> path), builds the EDM model with the
    /// appropriate container name, and wires the service document,
    /// <c>$metadata</c>, and per-set routes under <paramref name="rateLimitPolicy"/>.
    /// </summary>
    private static RouteGroupBuilder MapEndpointsCore(
        IEndpointRouteBuilder endpoints,
        string prefix,
        IReadOnlyList<ODataEntitySetDescriptor> descriptors,
        ODataFeedKind feedKind,
        string rateLimitPolicy,
        string containerName,
        string? metadataPermission,
        bool metadataStanceDeclared)
    {
        Dictionary<Type, ODataEntityTypeWhitelist> whitelistByType = ValidateAndResolveWhitelists(
            descriptors,
            endpoints.ServiceProvider,
            feedKind,
            metadataPermission,
            metadataStanceDeclared);

        IEdmModel edmModel = ODataEdmModelBuilder.Build(descriptors, whitelistByType, containerName);

        string tagSuffix = feedKind == ODataFeedKind.Host ? "OData (Host)" : "OData";
        string serviceDocOpName = feedKind == ODataFeedKind.Host ? "ODataHostServiceDocument" : "ODataServiceDocument";
        string metadataOpName = feedKind == ODataFeedKind.Host ? "ODataHostMetadata" : "ODataMetadata";

        RouteGroupBuilder root = endpoints.MapGranitGroup(prefix)
            .WithTags(tagSuffix)
            .RequireGranitRateLimiting(rateLimitPolicy);

        // #3005 — the schema-discovery documents live in a dedicated inner
        // group carrying the mount's explicit authorization stance. Anonymous
        // is a declared choice, never a default.
        RouteGroupBuilder metadataGroup = CreateMetadataGroup(root, metadataPermission);

        metadataGroup.MapODataServiceDocument("", edmModel)
            .WithName(serviceDocOpName)
            .WithSummary("OData v4 service document — lists every exposed EntitySet with its metadata link.")
            .WithDescription("BI clients (Power BI / Excel / Tableau) read this document to discover which EntitySets are available. Each set is queryable via the standard $filter / $select / $top / $skip / $orderby clauses.");

        metadataGroup.MapODataMetadata("$metadata", edmModel)
            .WithName(metadataOpName)
            .WithSummary("OData v4 EDM metadata document (CSDL XML).")
            .WithDescription("BI tools consume this CSDL document to populate their Navigator / table picker. The EDM is built from the application's registered QueryDefinition&lt;T&gt; instances.");

        foreach (ODataEntitySetDescriptor descriptor in descriptors)
        {
            MethodInfo mapper = typeof(ODataExposureEndpointRouteBuilderExtensions)
                .GetMethod(nameof(MapEntitySetRoute), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(descriptor.EntityType);

            mapper.Invoke(null, [root, descriptor, edmModel]);
        }

        return root;
    }

    /// <summary>
    /// Wraps the <c>$metadata</c> + service-document routes in an inner group
    /// carrying the mount's declared authorization stance (#3005). Gated:
    /// <c>RequireAuthorization()</c> + an endpoint filter resolving
    /// <see cref="IPermissionChecker"/> per request (same in-handler pattern
    /// as the entity routes), with 401/403 documented on the group. Anonymous:
    /// explicit <c>AllowAnonymous()</c> so a host-level fallback authorization
    /// policy cannot silently break BI discovery — the anonymity was declared.
    /// </summary>
    private static RouteGroupBuilder CreateMetadataGroup(RouteGroupBuilder root, string? metadataPermission)
    {
        RouteGroupBuilder group = root.MapGroup(string.Empty);

        if (metadataPermission is null)
        {
            group.AllowAnonymous();
            return group;
        }

        group
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                IPermissionChecker permissionChecker = context.HttpContext.RequestServices
                    .GetRequiredService<IPermissionChecker>();
                return await permissionChecker
                    .IsGrantedAsync(metadataPermission, context.HttpContext.RequestAborted)
                    .ConfigureAwait(false)
                    ? await next(context).ConfigureAwait(false)
                    : TypedResults.Forbid();
            })
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    /// <summary>
    /// Strict-config validator (C6 #1395, extended by ADR-050 and #3005).
    /// Refuses to start the host when a registered EntitySet hasn't
    /// acknowledged its security-sensitive configuration choices, when the
    /// mount lacks a <c>$metadata</c> authorization stance, or when a
    /// whitelisted <c>$expand</c> path is invalid — and resolves the
    /// per-CLR-type EDM whitelist map consumed by the EDM builder. The
    /// framework deliberately does NOT default-deny and does NOT silently
    /// apply safe defaults — those would let convention drift reach
    /// production unchecked. Failing fast at <c>Map*</c> time is the
    /// equivalent of an architecture test for a config surface that lives
    /// inside a closure (and is therefore not statically reflectable).
    /// </summary>
    /// <returns>Per-CLR-type whitelist covering every EntitySet root (scalars from its <c>ExportDefinition.GetFields()</c>) and every navigation-target type reachable through a whitelisted <c>$expand</c> path (transitive closure).</returns>
    /// <exception cref="InvalidOperationException">
    /// Any descriptor (or the mount) fails one of:
    /// (a) permission gate (missing or implicit anonymous);
    /// (b) <c>$expand</c>-intent gate (missing whitelist or DisableExpand);
    /// (c) host-feed gates (non-Host permission, missing AcknowledgeCrossTenantExposure on IMultiTenant);
    /// (d) ADR-050 gates: no registered <c>EntityDefinition</c> for the entity type, no <c>b.Export&lt;T&gt;()</c> reference, or no <c>IExportDefinitionDescriptor</c>;
    /// (e) #3005 gates: missing $metadata stance, $expand path segment that is not a navigation on its CLR type, or an $expand target type without a registered <c>IExportDefinitionDescriptor</c>.
    /// </exception>
    private static Dictionary<Type, ODataEntityTypeWhitelist> ValidateAndResolveWhitelists(
        IReadOnlyList<ODataEntitySetDescriptor> descriptors,
        IServiceProvider services,
        ODataFeedKind feedKind,
        string? metadataPermission,
        bool metadataStanceDeclared)
    {
        List<string> errors = [];
        Dictionary<Type, IReadOnlyList<string>> scalarsByType = [];
        Dictionary<Type, HashSet<string>> navigationsByType = [];

        IPermissionDefinitionRegistry? permissionDefinitions =
            feedKind == ODataFeedKind.Host
            && (metadataPermission is not null || descriptors.Any(d => d.RequiredPermission is not null))
                ? services.GetService<IPermissionDefinitionRegistry>()
                : null;

        ValidateMetadataStance(feedKind, metadataPermission, metadataStanceDeclared, permissionDefinitions, errors);

        // ADR-050 gates: resolve EntityDefinition + Export descriptors once.
        // Both abstractions live in their *.Abstractions packages so this
        // module avoids a hard dep on the runtime registries.
        IReadOnlyList<IEntityDefinitionDescriptor> entityDefinitions =
            [.. services.GetServices<IEntityDefinitionDescriptor>()];
        IReadOnlyList<IExportDefinitionDescriptor> exportDefinitions =
            [.. services.GetServices<IExportDefinitionDescriptor>()];

        foreach (ODataEntitySetDescriptor descriptor in descriptors)
        {
            ValidateSharedGates(descriptor, permissionDefinitions, errors);

            if (ResolveExportFields(descriptor, entityDefinitions, exportDefinitions, errors) is { } fields)
            {
                scalarsByType[descriptor.EntityType] = fields;
            }

            ValidateExpandClosure(descriptor, exportDefinitions, scalarsByType, navigationsByType, errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "OData EntitySet configuration is incomplete:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
        }

        Dictionary<Type, ODataEntityTypeWhitelist> whitelistByType = new(scalarsByType.Count);
        foreach ((Type type, IReadOnlyList<string> scalars) in scalarsByType)
        {
            IReadOnlyList<string> navigations =
                navigationsByType.TryGetValue(type, out HashSet<string>? set) ? [.. set] : [];
            whitelistByType[type] = new ODataEntityTypeWhitelist(scalars, navigations);
        }

        return whitelistByType;
    }

    /// <summary>
    /// #3005 — every mount must declare an explicit authorization stance for
    /// <c>$metadata</c> + the service document. Tenant-feed: either
    /// <c>RequireMetadataPermission(...)</c> or <c>AllowAnonymousMetadata()</c>.
    /// Host-feed: <c>RequireMetadataPermission(...)</c> is REQUIRED and the
    /// permission is validated to <see cref="MultiTenancySides.Host"/>,
    /// mirroring the entity-permission gate.
    /// </summary>
    private static void ValidateMetadataStance(
        ODataFeedKind feedKind,
        string? metadataPermission,
        bool metadataStanceDeclared,
        IPermissionDefinitionRegistry? permissionDefinitions,
        List<string> errors)
    {
        if (feedKind == ODataFeedKind.Tenant)
        {
            if (!metadataStanceDeclared)
            {
                errors.Add(
                    "The OData mount must declare an explicit authorization stance for $metadata and the service document — call options.RequireMetadataPermission(\"...\") or options.AllowAnonymousMetadata(). Both documents expose the mount's full schema (EntitySet names, columns, navigations); anonymous schema reconnaissance must be a declared choice, never a default (#3005).");
            }

            return;
        }

        if (metadataPermission is null)
        {
            errors.Add(
                "The OData host-feed mount must call options.RequireMetadataPermission(\"OData.Host.{Module}.Metadata.Read\") — the host-feed $metadata / service document expose the cross-tenant BI schema and have no anonymous variant (#3005).");
            return;
        }

        PermissionDefinition? permission = permissionDefinitions?.Find(metadataPermission);
        if (permission is null)
        {
            errors.Add(
                $"Host-feed $metadata requires permission '{metadataPermission}' but no PermissionDefinition with that name is registered. Declare it in an IPermissionDefinitionProvider with MultiTenancySides.Host before mounting the host-feed.");
        }
        else if (permission.MultiTenancySides != MultiTenancySides.Host)
        {
            errors.Add(
                $"Host-feed $metadata requires permission '{metadataPermission}' which is declared as MultiTenancySides.{permission.MultiTenancySides} — host-feed access requires MultiTenancySides.Host. Declare a dedicated host-side permission for the metadata documents.");
        }
    }

    /// <summary>
    /// Tenant- and host-feed shared gates: permission/anonymous intent, $expand intent,
    /// and host-feed-specific gates when applicable. Errors are appended in place.
    /// </summary>
    private static void ValidateSharedGates(
        ODataEntitySetDescriptor descriptor,
        IPermissionDefinitionRegistry? permissionDefinitions,
        List<string> errors)
    {
        if (descriptor.RequiredPermission is null && !descriptor.AnonymousAccessAcknowledged)
        {
            errors.Add(
                $"EntitySet '{descriptor.EntitySetName}' must call either RequirePermission(string) or AllowAnonymousAccess() — implicit anonymous OData access is rejected by the strict-config validator (C6 #1395). Convention: {RequiredPermissionConvention(descriptor)}.");
        }

        if (!descriptor.ExpandConfigurationAcknowledged)
        {
            errors.Add(
                $"EntitySet '{descriptor.EntitySetName}' must call either ExpandWhitelist(...) or DisableExpand() — implicit \"$expand disabled\" is rejected by the strict-config validator (C6 #1395). Use DisableExpand() to declare the intent, or ExpandWhitelist(\"NavProp1\", ...) to allow specific navigation paths.");
        }

        if (descriptor.FeedKind == ODataFeedKind.Host)
        {
            ValidateHostFeedGates(descriptor, permissionDefinitions, errors);
        }
    }

    /// <summary>
    /// #3005 / ADR-050 — walks every whitelisted <c>$expand</c> path segment
    /// by segment via reflection, validating that (a) each segment exists as
    /// a navigation-like property on the CLR type it is declared on, and
    /// (b) every navigation-target type has a registered
    /// <c>IExportDefinitionDescriptor</c> whose scalar fields become the
    /// target's EDM whitelist. Records the traversed navigation names per
    /// type (union across all descriptors) so the EDM builder exposes only
    /// the navigations that some whitelisted path actually walks. Errors are
    /// appended in place; a broken path stops at the first bad segment.
    /// </summary>
    private static void ValidateExpandClosure(
        ODataEntitySetDescriptor descriptor,
        IReadOnlyList<IExportDefinitionDescriptor> exportDefinitions,
        Dictionary<Type, IReadOnlyList<string>> scalarsByType,
        Dictionary<Type, HashSet<string>> navigationsByType,
        List<string> errors)
    {
        foreach (string path in descriptor.ExpandWhitelist ?? [])
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                ValidateExpandPath(path, descriptor, exportDefinitions, scalarsByType, navigationsByType, errors);
            }
        }
    }

    /// <summary>
    /// Walks one dotted <c>$expand</c> path segment by segment, recording the navigations it
    /// traverses and the scalar whitelist of every type it reaches. Stops at the first bad
    /// segment, appending the error in place.
    /// </summary>
    private static void ValidateExpandPath(
        string path,
        ODataEntitySetDescriptor descriptor,
        IReadOnlyList<IExportDefinitionDescriptor> exportDefinitions,
        Dictionary<Type, IReadOnlyList<string>> scalarsByType,
        Dictionary<Type, HashSet<string>> navigationsByType,
        List<string> errors)
    {
        Type currentType = descriptor.EntityType;
        foreach (string segment in path.Split('.', StringSplitOptions.TrimEntries))
        {
            PropertyInfo? navigation = Array.Find(
                currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                p => string.Equals(p.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (navigation is null || !ODataEdmModelBuilder.IsNavigationOrCollection(navigation.PropertyType))
            {
                errors.Add(
                    $"EntitySet '{descriptor.EntitySetName}' whitelists $expand path '{path}' but '{segment}' is not a navigation property on '{currentType.Name}'. Fix the path or remove it from ExpandWhitelist(...).");
                break;
            }

            if (!navigationsByType.TryGetValue(currentType, out HashSet<string>? navigations))
            {
                navigations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                navigationsByType[currentType] = navigations;
            }

            navigations.Add(navigation.Name);

            Type targetType = ODataEdmModelBuilder.ResolveNavigationTargetType(navigation.PropertyType);
            if (!scalarsByType.ContainsKey(targetType))
            {
                IExportDefinitionDescriptor? export = exportDefinitions
                    .FirstOrDefault(e => e.EntityType == targetType);
                if (export is null)
                {
                    errors.Add(
                        $"EntitySet '{descriptor.EntitySetName}' whitelists $expand path '{path}' reaching target type '{targetType.Name}', which has no registered IExportDefinitionDescriptor. Per ADR-050 every type reachable through $expand needs an export-derived scalar whitelist — register an ExportDefinition for {targetType.Name} (services.AddExportDefinition<{targetType.Name}, {targetType.Name}ExportDefinition>()) or remove the path.");
                    break;
                }

                scalarsByType[targetType] = ScalarFieldsOf(export);
            }

            currentType = targetType;
        }
    }

    /// <summary>
    /// ADR-050 gates #1, #2, #3: every EntitySet's entity must have a registered
    /// EntityDefinition, the EntityDefinition must reference an ExportDefinition via
    /// <c>b.Export&lt;T&gt;()</c>, and that Export must be DI-registered as
    /// <see cref="IExportDefinitionDescriptor"/>. Returns the resolved scalar field list,
    /// or <see langword="null"/> when any gate failed (error appended, descriptor skipped).
    /// </summary>
    private static IReadOnlyList<string>? ResolveExportFields(
        ODataEntitySetDescriptor descriptor,
        IReadOnlyList<IEntityDefinitionDescriptor> entityDefinitions,
        IReadOnlyList<IExportDefinitionDescriptor> exportDefinitions,
        List<string> errors)
    {
        IEntityDefinitionDescriptor? entityDefinition = entityDefinitions
            .FirstOrDefault(e => e.EntityType == descriptor.EntityType);
        if (entityDefinition is null)
        {
            errors.Add(
                $"EntitySet '{descriptor.EntitySetName}' targets entity '{descriptor.EntityType.Name}' which has no registered EntityDefinition. Per ADR-050, every OData EntitySet requires an EntityDefinition gate — register one via services.AddEntityDefinition<{descriptor.EntityType.Name}, {descriptor.EntityType.Name}EntityDefinition>() before mounting this set.");
            return null;
        }

        if (entityDefinition.Descriptor.ExportDefinitionType is null)
        {
            errors.Add(
                $"EntitySet '{descriptor.EntitySetName}' uses EntityDefinition '{entityDefinition.Name}' which does not declare a b.Export<T>() reference. Per ADR-050, the OData EDM whitelist is derived from the referenced ExportDefinition — add b.Export<{descriptor.EntityType.Name}ExportDefinition>() to the EntityDefinition's Configure method.");
            return null;
        }

        IExportDefinitionDescriptor? export = exportDefinitions
            .FirstOrDefault(e => e.EntityType == descriptor.EntityType);
        if (export is null)
        {
            errors.Add(
                $"EntitySet '{descriptor.EntitySetName}' references Export '{entityDefinition.Descriptor.ExportDefinitionType.Name}' but no IExportDefinitionDescriptor is registered for entity type '{descriptor.EntityType.Name}'. Did you forget services.AddExportDefinition<{descriptor.EntityType.Name}, {entityDefinition.Descriptor.ExportDefinitionType.Name}>()?");
            return null;
        }

        return ScalarFieldsOf(export);
    }

    /// <summary>
    /// Scalars only (flat paths, <c>IsNavigation == false</c>). Dotted export
    /// paths (<c>"Customer.Name"</c>) stay out of the EDM — navigations are
    /// modelled as OData NavigationProperties reached through the $expand
    /// whitelist closure, not flattened columns.
    /// </summary>
    private static IReadOnlyList<string> ScalarFieldsOf(IExportDefinitionDescriptor export) =>
        [.. export.GetFields()
            .Where(f => !f.IsNavigation && !f.PropertyPath.Contains('.', StringComparison.Ordinal))
            .Select(f => f.PropertyPath)];

    /// <summary>
    /// Host-feed-only gates: the permission must resolve to <see cref="MultiTenancySides.Host"/>,
    /// and any <c>IMultiTenant</c> entity must have called
    /// <c>AcknowledgeCrossTenantExposure(...)</c>.
    /// </summary>
    private static void ValidateHostFeedGates(
        ODataEntitySetDescriptor descriptor,
        IPermissionDefinitionRegistry? permissionDefinitions,
        List<string> errors)
    {
        if (descriptor.RequiredPermission is { } perm)
        {
            PermissionDefinition? permission = permissionDefinitions?.Find(perm);
            if (permission is null)
            {
                errors.Add(
                    $"Host-feed EntitySet '{descriptor.EntitySetName}' requires permission '{perm}' but no PermissionDefinition with that name is registered. Declare it in an IPermissionDefinitionProvider with MultiTenancySides.Host before mounting the host-feed.");
            }
            else if (permission.MultiTenancySides != MultiTenancySides.Host)
            {
                errors.Add(
                    $"Host-feed EntitySet '{descriptor.EntitySetName}' requires permission '{perm}' which is declared as MultiTenancySides.{permission.MultiTenancySides} — host-feed access requires MultiTenancySides.Host. Either declare a dedicated host-side permission, or move this EntitySet to the tenant-feed (MapGranitODataEndpoints).");
            }
        }

        if (typeof(IMultiTenant).IsAssignableFrom(descriptor.EntityType)
            && !descriptor.CrossTenantExposureAcknowledged)
        {
            errors.Add(
                $"Host-feed EntitySet '{descriptor.EntitySetName}' targets IMultiTenant entity '{descriptor.EntityType.Name}' but did not call AcknowledgeCrossTenantExposure(...). Without an explicit per-query bypass lambda, the framework's tenant filter (tenantId == currentTenant.Id) returns no rows for a tenantless caller — fail-closed. Add: .AcknowledgeCrossTenantExposure(q => q.IgnoreQueryFilters([GranitFilterNames.MultiTenant])).");
        }
    }

    /// <summary>Suggests the conventional <c>OData.{Module}.{Entity}.Read</c> (or <c>OData.Host.{Module}.{Entity}.Read</c> for host-feed) permission name, used in the strict-config error message.</summary>
    private static string RequiredPermissionConvention(ODataEntitySetDescriptor descriptor)
    {
        string segment = descriptor.EntityType.Namespace?.Split('.').LastOrDefault() ?? "Module";
        return descriptor.FeedKind == ODataFeedKind.Host
            ? $"OData.Host.{segment}.{descriptor.EntityType.Name}.Read"
            : $"OData.{segment}.{descriptor.EntityType.Name}.Read";
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
        // Captured once at Map time — typed Func used per-request without a cast.
        var crossTenantBypass = descriptor.CrossTenantBypass as Func<IQueryable<TEntity>, IQueryable<TEntity>>;

        // Per-route validation settings, computed once on first request (QueryDefinition
        // metadata is immutable) and shared by every subsequent request to this route.
        ODataValidationSettingsCache validationSettingsCache = new();

        // #3005 — resolved allowed-path set for the AST validator: every
        // whitelisted dotted path plus all of its prefixes, case-insensitive.
        FrozenSet<string> allowedExpandPaths = BuildAllowedExpandPaths(descriptor.ExpandWhitelist);

        RouteHandlerBuilder route = root.MapGet(descriptor.EntitySetName, async Task<object?> (
                ODataQueryOptions<TEntity> options,
                HttpContext httpContext,
                [FromServices] IQueryableSource<TEntity> source,
                [FromServices] IQueryEngine<TEntity> engine,
                [FromServices] IPermissionChecker permissionChecker,
                [FromServices] ODataExposureMetrics metrics,
                [FromServices] ICurrentTenant? currentTenant,
                CancellationToken cancellationToken) => await HandleEntitySetRequestAsync(
                    descriptor,
                    crossTenantBypass,
                    validationSettingsCache,
                    allowedExpandPaths,
                    options,
                    httpContext,
                    source,
                    engine,
                    permissionChecker,
                    metrics,
                    currentTenant,
                    cancellationToken).ConfigureAwait(false))
            .WithODataModel(edmModel)
            .WithODataResult()
            .WithODataOptions(opts => opts.SetMaxTop(descriptor.MaxTop));

        // Tenant-feed name stays `OData{Set}List` (no breaking change). Host-feed
        // gains a `Host` segment so an entity exposed on both feeds (e.g. canonical
        // User aggregate, ADR-051) does not collide on the globally-unique endpoint
        // name registry.
        string feedSegment = descriptor.FeedKind == ODataFeedKind.Host ? "Host" : string.Empty;
        route.WithName($"OData{feedSegment}{descriptor.EntitySetName}List")
             .WithSummary($"Returns the {descriptor.EntitySetName} EntitySet, filtered by the framework's tenant + soft-delete pipeline.")
             .WithDescription($"OData v4 endpoint for the {descriptor.EntitySetName} set. Supports $filter, $select, $top, $skip, $orderby. Tenant and soft-delete filters are applied BEFORE any user $filter — the OData query never bypasses framework access control. Per-set caps: MaxTop={descriptor.MaxTop}, PageSize={descriptor.PageSize}, $count={(descriptor.CountEnabled ? "enabled" : "disabled")}, $expand={(descriptor.ExpandWhitelist is null or { Count: 0 } ? "disabled" : string.Join(",", descriptor.ExpandWhitelist))}.")
             .Produces(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        // 401/403 are only reachable on gated sets — declaring them on an
        // AllowAnonymousAccess() set would be false documentation (#3005).
        if (descriptor.RequiredPermission is not null)
        {
            route.ProducesProblem(StatusCodes.Status401Unauthorized)
                 .ProducesProblem(StatusCodes.Status403Forbidden)
                 .RequireAuthorization();
        }
    }

    /// <summary>
    /// Expands the descriptor's dotted <c>$expand</c> whitelist into the full
    /// allowed-path set checked per request: every declared path AND every
    /// prefix of it (whitelisting <c>"Customer.Address"</c> implies plain
    /// <c>"Customer"</c> is expandable too). Case-insensitive to match OData's
    /// EnableCaseInsensitive semantics.
    /// </summary>
    internal static FrozenSet<string> BuildAllowedExpandPaths(IReadOnlyList<string>? whitelist)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string path in whitelist ?? [])
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string[] segments = path.Split('.', StringSplitOptions.TrimEntries);
            for (int length = 1; length <= segments.Length; length++)
            {
                paths.Add(string.Join('.', segments[..length]));
            }
        }

        return paths.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes one OData EntitySet GET: permission gate, $count / $expand rejection
    /// gates, MaxTop header emission, $filter → <see cref="QueryPredicate"/> translation
    /// (#3004), engine-side strict predicate enforcement, per-route OData validation, and
    /// finally the filtered queryable returned to the <c>WithODataResult</c> filter.
    /// Extracted from the route lambda to keep both the parameter list and the cognitive
    /// complexity tractable.
    /// </summary>
    private static async Task<object?> HandleEntitySetRequestAsync<TEntity>(
        ODataEntitySetDescriptor descriptor,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? crossTenantBypass,
        ODataValidationSettingsCache validationSettingsCache,
        FrozenSet<string> allowedExpandPaths,
        ODataQueryOptions<TEntity> options,
        HttpContext httpContext,
        IQueryableSource<TEntity> source,
        IQueryEngine<TEntity> engine,
        IPermissionChecker permissionChecker,
        ODataExposureMetrics metrics,
        ICurrentTenant? currentTenant,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (descriptor.RequiredPermission is { } perm
            && !await permissionChecker.IsGrantedAsync(perm, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Forbid();
        }

        (string? tenantTag, string feedKindTag) = ResolveMetricsTags(descriptor, currentTenant);

        if (TryRejectQueryShape(httpContext, options, descriptor, allowedExpandPaths, metrics, tenantTag, feedKindTag) is { } rejection)
        {
            return rejection;
        }

        ApplyMaxTopAppliedHeader(httpContext, descriptor, metrics, tenantTag, feedKindTag);

        // #1392 silent-clamp contract: an over-cap $top is pinned down to MaxTop BEFORE
        // validation so options.Validate() — which enforces the model-bound MaxTop set via
        // SetMaxTop and would otherwise return 400 — accepts it. ApplyTo applies the same cap,
        // and the OData-MaxTop-Applied header was already emitted above from the original value.
        options = ClampTopToCap(options, httpContext, descriptor.MaxTop);

        // #3004 — the user's $filter is translated into the engine's strict predicate tree
        // instead of being composed as arbitrary LINQ by ApplyTo. Untranslatable constructs
        // return 400 here; field-level violations return 400 from BuildFilteredQuery below.
        (QueryPredicate? predicate, ProblemHttpResult? filterRejection) =
            TranslateFilter(options, descriptor, metrics, tenantTag, feedKindTag);
        if (filterRejection is not null)
        {
            return filterRejection;
        }

        // Host-feed: apply the host-supplied per-query bypass BEFORE the QueryEngine
        // pipeline runs, so the QueryEngine sees an already-untenanted queryable.
        // Tenant-feed: no bypass; the QueryEngine receives the source as-is.
        IQueryable<TEntity> baseQueryable = source.GetQueryable();
        if (crossTenantBypass is not null)
        {
            baseQueryable = crossTenantBypass(baseQueryable);
        }

        IQueryable<TEntity> filtered;
        try
        {
            filtered = engine.BuildFilteredQuery(baseQueryable, new QueryRequest(), predicate);
        }
        catch (QueryPredicateValidationException ex)
        {
            // BREAKING (#3004): filtering on an EDM-visible but non-Filterable() column used
            // to pass through ApplyTo silently; the engine's strict validation now rejects it
            // with the offending fields listed.
            metrics.RecordRejectedQuery(descriptor.EntitySetName, "filter_field_rejected", tenantTag, feedKindTag);
            return TypedResults.Problem(
                detail: "The $filter was rejected by the query definition: "
                    + string.Join("; ", ex.Errors.Select(e =>
                        e.Field is null ? $"[{e.Code}] {e.Message}" : $"[{e.Code}] {e.Field}: {e.Message}")),
                statusCode: StatusCodes.Status400BadRequest,
                title: QueryOptionNotSupportedTitle);
        }

        ODataValidationSettings validationSettings = validationSettingsCache.GetOrCreate(
            () => CreateValidationSettings(descriptor, engine.GetMetadata()));
        try
        {
            options.Validate(validationSettings);
        }
        catch (ODataException ex)
        {
            metrics.RecordRejectedQuery(descriptor.EntitySetName, "odata_validation_failed", tenantTag, feedKindTag);
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: QueryOptionNotSupportedTitle);
        }

        ODataQuerySettings querySettings = new() { PageSize = descriptor.PageSize };

        // Return the raw IQueryable so the WithODataResult filter wraps it in an
        // ODataResult ({ "@odata.context": "...", "value": [...] }). TypedResults.Ok
        // would short-circuit the filter — its IResult-check returns the inner
        // Ok<IQueryable> unwrapped, which serializes as a bare JSON array and breaks
        // every BI-tool consumer expecting v4.
        // AllowedQueryOptions.Filter is the IGNORE flag: $filter was already enforced by
        // the engine above, so ApplyTo must never apply it a second time.
        return options.ApplyTo(filtered, querySettings, AllowedQueryOptions.Filter);
    }

    /// <summary>
    /// Translates <c>options.Filter</c> (when present) into the engine's strict
    /// <see cref="QueryPredicate"/> tree. Returns the predicate on success, or a
    /// <c>400</c> Problem when the clause fails to parse (<c>odata_validation_failed</c>)
    /// or contains constructs the translation layer rejects
    /// (<c>filter_not_translatable</c>).
    /// </summary>
    private static (QueryPredicate? Predicate, ProblemHttpResult? Rejection) TranslateFilter<TEntity>(
        ODataQueryOptions<TEntity> options,
        ODataEntitySetDescriptor descriptor,
        ODataExposureMetrics metrics,
        string? tenantTag,
        string feedKindTag)
        where TEntity : class
    {
        if (options.Filter is null)
        {
            return (null, null);
        }

        FilterClause filterClause;
        try
        {
            // FilterClause parses lazily — a malformed $filter (or one referencing a
            // property absent from the EDM) surfaces here as an ODataException.
            filterClause = options.Filter.FilterClause;
        }
        catch (ODataException ex)
        {
            metrics.RecordRejectedQuery(descriptor.EntitySetName, "odata_validation_failed", tenantTag, feedKindTag);
            return (null, TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: QueryOptionNotSupportedTitle));
        }

        ODataFilterTranslationResult translation = ODataFilterTranslator.Translate(filterClause);
        if (translation.RejectionDetail is not null)
        {
            metrics.RecordRejectedQuery(descriptor.EntitySetName, "filter_not_translatable", tenantTag, feedKindTag);
            return (null, TypedResults.Problem(
                detail: translation.RejectionDetail,
                statusCode: StatusCodes.Status400BadRequest,
                title: QueryOptionNotSupportedTitle));
        }

        return (translation.Predicate, null);
    }

    /// <summary>
    /// Builds the per-route <see cref="ODataValidationSettings"/> from the descriptor and the
    /// query definition's immutable metadata. <c>$orderby</c> is whitelisted from the
    /// definition's sortable columns; when the definition declares none, the OrderBy option is
    /// removed entirely (an empty <see cref="ODataValidationSettings.AllowedOrderByProperties"/>
    /// set means "allow all" upstream — the opposite of what an empty whitelist must mean).
    /// <c>MaxTop</c> is deliberately NOT set: the existing contract is a silent clamp via
    /// <c>SetMaxTop</c> (pinned by <c>QueryHardeningTests</c>), not a validation rejection.
    /// <c>MaxExpansionDepth</c> IS set from the descriptor (#3005) — the belt behind the AST
    /// depth check, and the layer that caps <c>$levels</c> literals the AST walk normalises.
    /// </summary>
    private static ODataValidationSettings CreateValidationSettings(
        ODataEntitySetDescriptor descriptor,
        QueryMetadata metadata)
    {
        // SkipToken stays allowed alongside Skip/Top: server-driven paging (PageSize) emits
        // @odata.nextLink continuations that BI clients follow with $skiptoken.
        AllowedQueryOptions allowed = AllowedQueryOptions.Filter
            | AllowedQueryOptions.Select
            | AllowedQueryOptions.Top
            | AllowedQueryOptions.Skip
            | AllowedQueryOptions.SkipToken;

        if (descriptor.CountEnabled)
        {
            allowed |= AllowedQueryOptions.Count;
        }

        if (descriptor.ExpandWhitelist is { Count: > 0 })
        {
            allowed |= AllowedQueryOptions.Expand;
        }

        ODataValidationSettings settings = new()
        {
            MaxAnyAllExpressionDepth = 1,
            MaxNodeCount = 100,
            MaxExpansionDepth = descriptor.MaxExpansionDepth,
        };

        if (metadata.SortableFields.Count > 0)
        {
            allowed |= AllowedQueryOptions.OrderBy;
            foreach (SortableField field in metadata.SortableFields)
            {
                settings.AllowedOrderByProperties.Add(field.Name);
            }
        }

        settings.AllowedQueryOptions = allowed;
        return settings;
    }

    /// <summary>
    /// Host-feed coalesces the tenant tag to <c>"global"</c> upstream so the metric
    /// dimension is never null. Tenant-feed reads the ambient
    /// <see cref="ICurrentTenant"/> (null when no tenant is resolved yet).
    /// </summary>
    private static (string? TenantTag, string FeedKindTag) ResolveMetricsTags(
        ODataEntitySetDescriptor descriptor,
        ICurrentTenant? currentTenant)
    {
        if (descriptor.FeedKind == ODataFeedKind.Host)
        {
            return ("global", "host");
        }

        string? tenantTag = currentTenant is { IsAvailable: true, Id: { } tid } ? tid.ToString() : null;
        return (tenantTag, "tenant");
    }

    /// <summary>
    /// Combines the $count and $expand gates into one rejection probe — keeps the
    /// route handler flat and records the matching metric reason on rejection.
    /// </summary>
    private static ProblemHttpResult? TryRejectQueryShape<TEntity>(
        HttpContext httpContext,
        ODataQueryOptions<TEntity> options,
        ODataEntitySetDescriptor descriptor,
        FrozenSet<string> allowedExpandPaths,
        ODataExposureMetrics metrics,
        string? tenantTag,
        string feedKindTag)
        where TEntity : class
    {
        if (RejectIfCountDisallowed(httpContext, descriptor) is { } countRejection)
        {
            metrics.RecordRejectedQuery(descriptor.EntitySetName, "count_disabled", tenantTag, feedKindTag);
            return countRejection;
        }

        (ProblemHttpResult? expandRejection, string expandReason) =
            RejectIfExpandUnauthorised(options, descriptor, allowedExpandPaths);
        if (expandRejection is not null)
        {
            metrics.RecordRejectedQuery(descriptor.EntitySetName, expandReason, tenantTag, feedKindTag);
            return expandRejection;
        }

        return null;
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
    /// #3005 — validates the user's <c>$expand</c> against the EntitySet's
    /// resolved path whitelist and <see cref="ODataEntitySetDescriptor.MaxExpansionDepth"/>
    /// by walking the parsed <see cref="SelectExpandClause"/> AST recursively
    /// (replaces the former string parser, which only saw top-level
    /// segments). Empty whitelist → <c>$expand</c> rejected outright.
    /// Returns the rejection plus the stable metric reason
    /// (<c>expand_not_whitelisted</c> / <c>expand_depth_exceeded</c> /
    /// <c>odata_validation_failed</c> for unparsable clauses).
    /// </summary>
    private static (ProblemHttpResult? Rejection, string Reason) RejectIfExpandUnauthorised<TEntity>(
        ODataQueryOptions<TEntity> options,
        ODataEntitySetDescriptor descriptor,
        FrozenSet<string> allowedExpandPaths)
        where TEntity : class
    {
        if (options.SelectExpand is not { RawExpand: { } rawExpand }
            || string.IsNullOrWhiteSpace(rawExpand))
        {
            return (null, string.Empty);
        }

        if (allowedExpandPaths.Count == 0)
        {
            return (TypedResults.Problem(
                detail: $"$expand is not enabled on the '{descriptor.EntitySetName}' EntitySet.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Query option not allowed"), "expand_not_whitelisted");
        }

        SelectExpandClause clause;
        try
        {
            // SelectExpandClause parses lazily — a malformed $expand (or one
            // referencing a navigation absent from the EDM, which after the
            // #3005 startup gates means "absent from the whitelist closure")
            // surfaces here as an ODataException.
            clause = options.SelectExpand.SelectExpandClause;
        }
        catch (ODataException ex)
        {
            return (TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: QueryOptionNotSupportedTitle), "odata_validation_failed");
        }

        return ValidateExpandItems(clause.SelectedItems, prefix: null, depth: 0, descriptor, allowedExpandPaths);
    }

    /// <summary>
    /// Recursive AST walk over the expand items of one
    /// <see cref="SelectExpandClause"/> level. Builds the dotted path for
    /// every <see cref="ExpandedReferenceSelectItem"/> (which covers
    /// <see cref="ExpandedNavigationSelectItem"/> and <c>$ref</c> expands),
    /// checks depth against <see cref="ODataEntitySetDescriptor.MaxExpansionDepth"/>
    /// FIRST (a too-deep path is a depth problem even when un-whitelisted),
    /// then membership in the resolved allowed-path set. <c>$levels</c>
    /// literals are normalised into extra depth AND extra repeated path
    /// segments — <c>$expand=Manager($levels=2)</c> is equivalent to
    /// <c>Manager.Manager</c> and both spellings pass through the same gates.
    /// </summary>
    internal static (ProblemHttpResult? Rejection, string Reason) ValidateExpandItems(
        IEnumerable<SelectItem> items,
        string? prefix,
        int depth,
        ODataEntitySetDescriptor descriptor,
        FrozenSet<string> allowedExpandPaths)
    {
        foreach (ExpandedReferenceSelectItem item in items.OfType<ExpandedReferenceSelectItem>())
        {
            (ProblemHttpResult? Rejection, string Reason) result =
                ValidateExpandItem(item, prefix, depth, descriptor, allowedExpandPaths);
            if (result.Rejection is not null)
            {
                return result;
            }
        }

        return (null, string.Empty);
    }

    /// <summary>
    /// Gates a single expand item: depth first (a too-deep path is a depth problem even when
    /// un-whitelisted), then whitelist membership for the path and each <c>$levels</c>
    /// repetition, then recursion into its own nested expands.
    /// </summary>
    private static (ProblemHttpResult? Rejection, string Reason) ValidateExpandItem(
        ExpandedReferenceSelectItem item,
        string? prefix,
        int depth,
        ODataEntitySetDescriptor descriptor,
        FrozenSet<string> allowedExpandPaths)
    {
        IReadOnlyList<string> navSegments = [.. item.PathToNavigationProperty
            .OfType<NavigationPropertySegment>()
            .Select(s => s.NavigationProperty.Name)];
        if (navSegments.Count == 0)
        {
            return (null, string.Empty);
        }

        string path = prefix is null
            ? string.Join('.', navSegments)
            : $"{prefix}.{string.Join('.', navSegments)}";
        int itemDepth = depth + navSegments.Count;

        long extraLevels = (item as ExpandedNavigationSelectItem)?.LevelsOption switch
        {
            null => 0,
            { IsMaxLevel: true } => long.MaxValue,
            { } levels => levels.Level - 1,
        };

        if (extraLevels == long.MaxValue || itemDepth + extraLevels > descriptor.MaxExpansionDepth)
        {
            return (TypedResults.Problem(
                detail: $"$expand nesting depth at '{path}' exceeds the maximum of {descriptor.MaxExpansionDepth} on the '{descriptor.EntitySetName}' EntitySet.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Expand depth exceeded"), "expand_depth_exceeded");
        }

        // $levels repeats the LAST navigation segment — each repetition is
        // a deeper dotted path and must be whitelisted like explicit nesting.
        string levelPath = path;
        for (long level = 0; level <= extraLevels; level++)
        {
            if (!allowedExpandPaths.Contains(levelPath))
            {
                return (TypedResults.Problem(
                    detail: $"$expand of '{levelPath}' is not permitted on the '{descriptor.EntitySetName}' EntitySet. Allowed paths: {string.Join(", ", allowedExpandPaths.Order(StringComparer.OrdinalIgnoreCase))}.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Expand path not whitelisted"), "expand_not_whitelisted");
            }

            levelPath = $"{levelPath}.{navSegments[^1]}";
        }

        if (item is ExpandedNavigationSelectItem { SelectAndExpand: { } nested })
        {
            (ProblemHttpResult? Rejection, string Reason) nestedResult =
                ValidateExpandItems(nested.SelectedItems, path, itemDepth, descriptor, allowedExpandPaths);
            if (nestedResult.Rejection is not null)
            {
                return nestedResult;
            }
        }

        return (null, string.Empty);
    }

    /// <summary>
    /// Sets the <c>OData-MaxTop-Applied</c> response header when the user's
    /// <c>$top</c> exceeded the descriptor's cap — the framework clamps
    /// silently (per acceptance criteria), the header surfaces the clamping
    /// to observability tooling. Also bumps the rejected-query counter for
    /// the same reason.
    /// </summary>
    /// <summary>
    /// Pins an over-cap user <c>$top</c> down to the EntitySet's
    /// <see cref="ODataEntitySetDescriptor.MaxTop"/> by rewriting the request query string and
    /// re-parsing the options. Preserves the historical silent-clamp contract (#1392): a
    /// <c>$top</c> above the cap is served capped, not rejected — the downstream
    /// <c>options.Validate</c> enforces the model-bound MaxTop and would otherwise return 400.
    /// No-op when <c>$top</c> is absent or already within the cap.
    /// </summary>
    private static ODataQueryOptions<TEntity> ClampTopToCap<TEntity>(
        ODataQueryOptions<TEntity> options,
        HttpContext httpContext,
        int maxTop)
        where TEntity : class
    {
        if (options.Top is not { Value: int requested } || requested <= maxTop)
        {
            return options;
        }

        List<KeyValuePair<string, string?>> query = [];
        foreach ((string key, StringValues values) in httpContext.Request.Query)
        {
            if (string.Equals(key, "$top", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string? value in values)
            {
                query.Add(new(key, value));
            }
        }

        query.Add(new("$top", maxTop.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        httpContext.Request.QueryString = QueryString.Create(query);

        return new ODataQueryOptions<TEntity>(options.Context, httpContext.Request);
    }

    private static void ApplyMaxTopAppliedHeader(
        HttpContext httpContext,
        ODataEntitySetDescriptor descriptor,
        ODataExposureMetrics metrics,
        string? tenantTag,
        string feedKindTag)
    {
        if (!httpContext.Request.Query.TryGetValue("$top", out StringValues topRaw)
            || !int.TryParse(topRaw.ToString(), out int requestedTop)
            || requestedTop <= descriptor.MaxTop)
        {
            return;
        }

        httpContext.Response.Headers[MaxTopAppliedHeader] =
            descriptor.MaxTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
        metrics.RecordTopClamped(descriptor.EntitySetName, tenantTag, feedKindTag);
    }
}
