using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Authorization.Extensions;
using Granit.QueryEngine;
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
        group.MapGet("/", ListAsync)
            .WithName("ListApiKeys")
            .WithSummary("Returns a paginated list of API keys.")
            .WithDescription("Lists all API keys for the current tenant with optional filters on type, environment, search term, and revocation status. The raw secret is never returned — only the prefix and last four characters for identification.")
            .Produces<PagedResult<ApiKeyResponse>>()
            .AllowHostAccess();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetApiKeyById")
            .WithSummary("Returns a single API key by ID.")
            .WithDescription("Returns the metadata of a single API key. The raw secret is never exposed after creation. Returns 404 if the key does not exist.")
            .Produces<ApiKeyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Ok<PagedResult<ApiKeyResponse>>> ListAsync(
        [FromServices] IApiKeyAdminStore adminStore,
        [AsParameters] ApiKeyListRequest request,
        CancellationToken cancellationToken)
    {
        (int page, int pageSize) = QueryEngineDefaults.ClampPagination(request.Page, request.PageSize);

        PagedResult<ApiKeyEntry> result = await adminStore.ListAsync(
            request.Search,
            request.Type,
            request.Environment,
            request.IncludeRevoked,
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);

        var items = result.Items.Select(ApiKeyResponse.FromEntry).ToList();

        return TypedResults.Ok(new PagedResult<ApiKeyResponse>(items, result.TotalCount, result.HasMore));
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
