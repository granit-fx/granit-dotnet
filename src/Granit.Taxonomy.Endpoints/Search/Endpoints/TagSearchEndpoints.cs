using Granit.Taxonomy.Endpoints.Permissions;
using Granit.Taxonomy.Endpoints.Search.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Taxonomy.Endpoints.Search.Endpoints;

/// <summary>
/// HTTP endpoint for the cross-entity tag search introduced by ADR-054 (T3.1):
/// answers "show me everything tagged X" with results grouped by <c>TargetType</c>.
/// </summary>
internal static class TagSearchEndpoints
{
    private const string TagName = "Taxonomy";

    public static RouteGroupBuilder MapTagSearchEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder search = group.MapGroup("/search").WithTags(TagName);

        search.MapGet("/", SearchAsync)
            .WithName("SearchTaxonomy")
            .WithSummary("Cross-entity tag search grouped by target type.")
            .WithDescription(
                "Returns tags whose Name prefix-matches q (case-insensitive) along "
                + "with the assignments grouped by TargetType so the frontend can "
                + "dispatch to the matching rendering pipeline. Pass scope=* for "
                + "cross-scope, otherwise the search is restricted to the matching "
                + "Tag.Scope. SECURITY: results expose TargetIds the caller may not "
                + "have per-entity read on — hosts MUST combine with their own "
                + "per-entity ACL when rendering.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Search.Read))
            .Produces<SearchResponse>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Results<Ok<SearchResponse>, ValidationProblem>> SearchAsync(
        [FromQuery] string q,
        [FromQuery] string? scope,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        [FromServices] ITagSearchService service,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new();
        if (string.IsNullOrWhiteSpace(q))
        {
            errors[nameof(q)] = ["q is required."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        TagSearchResult result = await service
            .SearchAsync(q, scope ?? "*", skip ?? 0, take ?? 50, cancellationToken)
            .ConfigureAwait(false);

        SearchResponse response = new(
            [.. result.Tags.Select(t => new SearchTagItem(t.Id, t.Name, t.Color, t.Scope))],
            result.HitsByTargetType.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<SearchHit>)[.. kvp.Value.Select(h => new SearchHit(h.TargetId, h.TagIds))],
                StringComparer.Ordinal),
            result.TotalCount,
            result.Skip,
            result.Take);

        return TypedResults.Ok(response);
    }
}
