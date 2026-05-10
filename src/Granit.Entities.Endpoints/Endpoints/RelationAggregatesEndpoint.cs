using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Granit.Entities.Diagnostics;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Granit.Entities.Endpoints.Options;
using Granit.Entities.Relations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Entities.Endpoints.Endpoints;

/// <summary>
/// <c>POST /api/entities/{name}/{id}/relations/aggregates</c> — returns the
/// aggregate values (count / sum / avg / min / max) for the relations of a
/// single source row in one round-trip (story #1561). Wires permission
/// filtering on the relation list (story #1562) and FusionCache 30-second
/// sliding per (source, id, relation, perms-hash, culture).
/// </summary>
internal static class RelationAggregatesEndpoint
{
    /// <summary>
    /// Mounts the route on the provided group. Called from
    /// <c>MapGranitEntitiesEndpoints</c> alongside the discovery + manifest routes.
    /// </summary>
    public static RouteGroupBuilder MapRelationAggregatesEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{name}/{id}/relations/aggregates", HandleAsync)
            .WithName("PostEntityRelationAggregates")
            .WithSummary("Returns the aggregate values for the relations of a single source row, in one round-trip.")
            .WithDescription("Computes count / sum / avg / min / max per relation declared on the source EntityDefinition. Pass `relations: []` (or omit the body) to get every relation the caller can read; pass an explicit list to slim the response. Each relation is computed in parallel by the registered IRelationAggregateService — the framework default returns an empty dictionary; hosts plug in an EF Core implementation. Defense-in-depth: relations gated by a permission the caller does not hold are absent from the request set and the response. FusionCache 30-second sliding per (sourceEntity, sourceId, relation, perms-hash, culture).")
            .Produces<RelationAggregatesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<Results<Ok<RelationAggregatesResponse>, ProblemHttpResult>> HandleAsync(
        string name,
        string id,
        RelationAggregatesRequest? request,
        [FromServices] IEntityDefinitionRegistry registry,
        [FromServices] EntityPermissionResolver permissionResolver,
        [FromServices] IRelationAggregateService aggregateService,
        [FromServices] IFusionCache cache,
        [FromServices] IOptions<EntitiesEndpointsOptions> options,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
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
            return TypedResults.Problem(
                detail: $"You do not have permission to read entity '{name}'.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        IReadOnlyList<RelationDescriptor> visibleRelations = await ResolveVisibleRelationsAsync(
            descriptor, request?.Relations, permissionResolver, cancellationToken).ConfigureAwait(false);

        if (visibleRelations.Count == 0)
        {
            return TypedResults.Ok(new RelationAggregatesResponse(
                new Dictionary<string, RelationAggregateValue>(StringComparer.Ordinal)));
        }

        IReadOnlyDictionary<string, RelationAggregateValue> aggregates = await ComputeWithCacheAsync(
            descriptor.Name, id, visibleRelations, aggregateService, cache, user, options.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new RelationAggregatesResponse(aggregates));
    }

    /// <summary>
    /// Resolves the requested relation list down to the relations the caller
    /// is allowed to see (defense in depth, story #1562). When the caller
    /// passes <see langword="null"/> or an empty list, defaults to every
    /// readable relation on the source.
    /// </summary>
    private static async Task<IReadOnlyList<RelationDescriptor>> ResolveVisibleRelationsAsync(
        EntityDefinitionDescriptor descriptor,
        IReadOnlyList<string>? requested,
        EntityPermissionResolver permissionResolver,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RelationDescriptor> candidates = requested is null || requested.Count == 0
            ? descriptor.Relations
            : [.. descriptor.Relations.Where(r => requested.Contains(r.Name, StringComparer.Ordinal))];

        if (candidates.Count == 0)
        {
            return [];
        }

        // Resolve every distinct RequiresPermission referenced by the
        // candidates in one batch. Relations with no gate fall through.
        HashSet<string> referenced = new(
            candidates.Where(r => r.RequiresPermission is not null).Select(r => r.RequiresPermission!),
            StringComparer.Ordinal);

        if (referenced.Count == 0)
        {
            return candidates;
        }

        IReadOnlyList<string> granted = await permissionResolver
            .CheckBatchAsync(referenced, cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> grantedSet = new(granted, StringComparer.Ordinal);

        return [.. candidates.Where(r =>
            r.RequiresPermission is null || grantedSet.Contains(r.RequiresPermission))];
    }

    /// <summary>
    /// Fans the visible relations through FusionCache. Cache misses go
    /// through the registered <see cref="IRelationAggregateService"/> in
    /// parallel; cache hits are returned without re-computation.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, RelationAggregateValue>> ComputeWithCacheAsync(
        string sourceEntityName,
        string sourceId,
        IReadOnlyList<RelationDescriptor> visible,
        IRelationAggregateService service,
        IFusionCache cache,
        ClaimsPrincipal user,
        EntitiesEndpointsOptions options,
        CancellationToken cancellationToken)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;

        // Pass 1: discover which relations are already cached.
        Dictionary<string, RelationAggregateValue> cached = new(StringComparer.Ordinal);
        List<string> misses = [];

        foreach (RelationDescriptor relation in visible)
        {
            string key = RelationAggregateCacheKey.Build(
                sourceEntityName, sourceId, relation.Name, user, culture);

            MaybeValue<RelationAggregateValue> maybe = cache.TryGet<RelationAggregateValue>(
                key, token: cancellationToken);

            if (maybe.HasValue)
            {
                cached[relation.Name] = maybe.Value;
            }
            else
            {
                misses.Add(relation.Name);
            }
        }

        // Pass 2: compute the misses in one runner call (the runner is
        // expected to fan them in parallel internally — story #1561 SLA).
        if (misses.Count > 0)
        {
            using Activity? activity = EntityActivitySource.Source.StartActivity("relations.aggregates.compute");
            activity?.SetTag("source_entity", sourceEntityName);
            activity?.SetTag("relation_count", misses.Count);

            IReadOnlyDictionary<string, RelationAggregateValue> computed = await service
                .ComputeAsync(sourceEntityName, sourceId, misses, user, cancellationToken)
                .ConfigureAwait(false);

            FusionCacheEntryOptions entryOptions = new() { Duration = options.RelationAggregatesCacheTtl };
            string sourceTag = RelationAggregateCacheKey.EvictionTag(sourceEntityName, sourceId);

            foreach ((string relName, RelationAggregateValue value) in computed)
            {
                string key = RelationAggregateCacheKey.Build(
                    sourceEntityName, sourceId, relName, user, culture);
                // Two tags per entry: the per-(source, id) tag drops every relation
                // when the source row changes; the per-(source, relation) tag drops
                // every cached counter for one relation when any row of the related
                // entity changes (story #1793).
                string relationTag = RelationAggregateCacheKey.EvictionTagForRelation(sourceEntityName, relName);
                await cache.SetAsync(key, value, entryOptions, [sourceTag, relationTag], token: cancellationToken)
                    .ConfigureAwait(false);
                cached[relName] = value;
            }
        }

        return cached;
    }
}
