using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Authorization.Extensions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authentication.ApiKeys.Endpoints.Endpoints;

/// <summary>
/// Endpoints for listing and viewing API keys.
/// </summary>
internal static class ApiKeyReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        // List + metadata: served by the query engine (filtering, sorting, search, pagination).
        // GET /        → PagedResult<ApiKeyListItemResponse> (summary; never exposes the secret)
        // GET /meta    → query metadata for client-driven grids
        // Revoked keys are hidden by default (the "active" quick filter is default); request
        // ?quickFilters=includeRevoked to include them. See ApiKeyEntryQueryDefinition.
        group.MapGranitQuery<ApiKeyEntry>().AllowHostAccess();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetApiKeyById")
            .WithSummary("Returns a single API key by ID.")
            .WithDescription("Returns the metadata of a single API key. The raw secret is never exposed after creation. Returns 404 if the key does not exist.")
            .Produces<ApiKeyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Results<Ok<ApiKeyResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IApiKeyAdminStore adminStore,
        CancellationToken cancellationToken)
    {
        ApiKeyEntry? entry = await adminStore.FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ApiKeyResponse.FromEntry(entry));
    }
}
