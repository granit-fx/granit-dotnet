using System.Globalization;
using System.Security.Claims;
using Granit.Entities.Details;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Granit.Entities.Endpoints.Options;
using Granit.Entities.Forms;
using Granit.Entities.Internal;
using Granit.Entities.Manifests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Entities.Endpoints.Endpoints;

/// <summary>
/// Minimal API handlers for the entity-manifest surface (Phase 1.C).
/// Mounted by <c>MapGranitEntitiesEndpoints</c> under the configured prefix.
/// </summary>
internal static class EntitiesEndpoints
{
    public static RouteGroupBuilder MapEntitiesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", DiscoveryAsync)
            .WithName("GetEntitiesDiscovery")
            .WithSummary("Returns the discovery tree of registered entities, grouped by module.")
            .WithDescription("Lists every EntityDefinition the requesting user has Read permission on (or that has no PermissionGroup declared), grouped by module. Each item carries a Manifest link plus a List link when the entity exposes a query collection. Empty modules are omitted. Cached 5 minutes per (user-perms-hash, culture).")
            .Produces<EntityDiscoveryResponse>();

        group.MapGet("/{name}", ManifestAsync)
            .WithName("GetEntityManifest")
            .WithSummary("Returns the per-entity manifest for the requesting user.")
            .WithDescription("Aggregates identity, permission flags, form variants, detail variants, and collection references (query / export / metric / dashboard / default view) for the addressed entity. Defense-in-depth: fields, sections, side panels gated by a permission the user does NOT hold are absent from the payload, never just hidden. Use ?facets=... to slim the response. Carries a strong ETag and the Granit-Entities-Schema-Version header; honours If-None-Match to short-circuit with 304.")
            .Produces<EntityManifestResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapRelationAggregatesEndpoint();
        group.MapCalendarRangeEndpoint();

        return group;
    }

    private static async Task<Results<Ok<EntityDiscoveryResponse>, ProblemHttpResult>> DiscoveryAsync(
        [FromServices] IEntityDefinitionRegistry registry,
        [FromServices] EntityPermissionResolver permissionResolver,
        [FromServices] IFusionCache cache,
        [FromServices] IOptions<EntitiesEndpointsOptions> options,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        string cacheKey = EntityCacheKey.ForDiscovery(user, culture);

        EntityDiscoveryResponse response = await cache.GetOrSetAsync(
            cacheKey,
            async ct => await BuildDiscoveryAsync(registry, permissionResolver, ct).ConfigureAwait(false),
            new FusionCacheEntryOptions { Duration = options.Value.DiscoveryCacheTtl },
            token: cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(response);
    }

    private static async Task<EntityDiscoveryResponse> BuildDiscoveryAsync(
        IEntityDefinitionRegistry registry,
        EntityPermissionResolver permissionResolver,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<EntityDiscoveryItemResponse>> byModule = new(StringComparer.Ordinal);

        foreach (IEntityDefinitionDescriptor descriptorRef in registry.All)
        {
            EntityDefinitionDescriptor descriptor = descriptorRef.Descriptor;
            EntityPermissionSnapshot snapshot = await permissionResolver
                .ResolveAsync(descriptor.PermissionGroup, cancellationToken)
                .ConfigureAwait(false);

            if (!snapshot.IsVisible)
            {
                continue;
            }

            string module = EntityModuleResolver.Resolve(descriptor.Name);
            if (!byModule.TryGetValue(module, out List<EntityDiscoveryItemResponse>? items))
            {
                items = [];
                byModule[module] = items;
            }

            string? listLink = descriptor.QueryDefinitionType is null
                ? null
                : $"/api/{Uri.EscapeDataString(descriptor.Name)}/list";

            items.Add(new EntityDiscoveryItemResponse(
                descriptor.Name,
                descriptor.DisplayKey,
                descriptor.Icon,
                descriptor.PermissionGroup,
                new EntityDiscoveryLinks(
                    Manifest: $"/api/entities/{Uri.EscapeDataString(descriptor.Name)}",
                    List: listLink)));
        }

        IReadOnlyList<EntityModuleGroupResponse> modules = byModule
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new EntityModuleGroupResponse(
                kv.Key,
                kv.Value.OrderBy(i => i.Name, StringComparer.Ordinal).ToList()))
            .ToList();

        return new EntityDiscoveryResponse(EntityManifestComposer.SchemaVersion, modules);
    }

    private static async Task<Results<Ok<EntityManifestResponse>, StatusCodeHttpResult, ProblemHttpResult>> ManifestAsync(
        string name,
        [FromQuery] string? facets,
        [FromServices] IEntityDefinitionRegistry registry,
        [FromServices] EntityPermissionResolver permissionResolver,
        [FromServices] IFusionCache cache,
        [FromServices] IOptions<EntitiesEndpointsOptions> options,
        [FromServices] IManifestCustomizationApplier customizationApplier,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        // Optional — null when the host has not loaded Granit.Activities runtime;
        // the manifest then omits the activities section entirely (ADR-045 §3).
        [FromServices] Granit.Activities.IActivityRegistry? activityRegistry = null)
    {
        IEntityDefinitionDescriptor? definitionRef = registry.GetByName(name);
        if (definitionRef is null)
        {
            return TypedResults.Problem(
                detail: $"No EntityDefinition is registered with name '{name}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        EntityDefinitionDescriptor descriptor = definitionRef.Descriptor;
        EntityPermissionSnapshot snapshot = await permissionResolver
            .ResolveAsync(descriptor.PermissionGroup, cancellationToken)
            .ConfigureAwait(false);

        if (!snapshot.IsVisible)
        {
            // 403 — defense-in-depth: same shape as a denied authorization
            // policy, no manifest leakage to callers without read access.
            return TypedResults.Problem(
                detail: $"You do not have permission to read entity '{name}'.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        EntityFacets selected = EntityFacetParser.Parse(facets);
        CultureInfo culture = CultureInfo.CurrentUICulture;
        string cacheKey = EntityCacheKey.ForManifest(name, user, culture)
            + $":{(int)selected}";

        EntityManifestResponse manifest = await cache.GetOrSetAsync(
            cacheKey,
            async ct =>
            {
                IReadOnlySet<string> grantedPermissions = await CollectGrantedPermissionsAsync(
                    permissionResolver, descriptor, ct).ConfigureAwait(false);
                EntityManifestResponse composed = EntityManifestComposer.Compose(
                    descriptor, snapshot, grantedPermissions, selected, defaultViewId: null, activityRegistry);
                // Layer 4 (ADR-053): apply tenant customization deltas + provenance.
                // Default impl is a no-op; replaced by Granit.Entities.Customization.Endpoints
                // when that package is loaded.
                return await customizationApplier.ApplyAsync(name, composed, ct).ConfigureAwait(false);
            },
            new FusionCacheEntryOptions { Duration = options.Value.ManifestCacheTtl },
            tags: [EntityCacheKey.EvictionTagForManifest(name)],
            token: cancellationToken)
            .ConfigureAwait(false);

        string etag = EntityManifestETag.Compute(manifest);
        httpContext.Response.Headers["Granit-Entities-Schema-Version"] =
            EntityManifestComposer.SchemaVersion.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers.ETag = etag;

        if (httpContext.Request.Headers.IfNoneMatch.ToString() is { Length: > 0 } ifNoneMatch
            && string.Equals(ifNoneMatch, etag, StringComparison.Ordinal))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        return TypedResults.Ok(manifest);
    }

    /// <summary>
    /// Collects the granted-permission strings the manifest needs to gate
    /// fields / sections / side panels. The set covers (a) the entity's
    /// standard <c>{Group}.{Action}</c> bundle and (b) every
    /// <c>RequiresPermission</c> referenced by descriptors on this entity.
    /// </summary>
    private static async Task<IReadOnlySet<string>> CollectGrantedPermissionsAsync(
        EntityPermissionResolver permissionResolver,
        EntityDefinitionDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        HashSet<string> referenced = new(StringComparer.Ordinal);
        foreach (FormDescriptor form in descriptor.Forms)
        {
            foreach (SectionDescriptor section in form.Sections)
            {
                foreach (FieldDescriptor field in section.Fields)
                {
                    if (field.RequiresPermission is { } perm)
                    {
                        referenced.Add(perm);
                    }
                }
            }
        }

        foreach (DetailDescriptor detail in descriptor.Details)
        {
            foreach (SidePanelDescriptor panel in detail.SidePanels)
            {
                if (panel.RequiresPermission is { } perm)
                {
                    referenced.Add(perm);
                }
            }
        }

        foreach (Granit.Entities.Relations.RelationDescriptor relation in descriptor.Relations
            .Where(static r => r.RequiresPermission is not null))
        {
            referenced.Add(relation.RequiresPermission!);
        }

        if (referenced.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        IReadOnlyList<string> granted = await permissionResolver
            .CheckBatchAsync(referenced, cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(granted, StringComparer.Ordinal);
    }
}
