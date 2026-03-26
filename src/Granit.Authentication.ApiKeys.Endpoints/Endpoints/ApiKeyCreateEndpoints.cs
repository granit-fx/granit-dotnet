using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Authorization.Abstractions;
using Granit.Guids;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authentication.ApiKeys.Endpoints.Endpoints;

/// <summary>
/// Endpoint for creating new API keys.
/// </summary>
internal static class ApiKeyCreateEndpoints
{
    internal static RouteGroupBuilder MapCreateEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAsync)
            .WithName("CreateApiKey")
            .WithSummary("Creates a new API key. The raw secret is returned once.")
            .WithDescription("Generates a new API key with the specified type, environment, permissions, and optional CIDR restrictions. The caller must possess every permission being assigned (privilege escalation prevention). The response includes the full raw secret — this is the only time the secret is available. Store it securely; it cannot be retrieved later. The key is immediately active.")
            .Produces<ApiKeyCreateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<Results<Created<ApiKeyCreateResponse>, ProblemHttpResult>> CreateAsync(
        ApiKeyCreateRequest request,
        [FromServices] IApiKeyGenerator generator,
        [FromServices] IApiKeyAdminStore adminStore,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        // Validate that the caller possesses every permission being assigned (OWASP API5:2023)
        foreach (string permission in request.Permissions ?? [])
        {
            if (!await permissionChecker.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false))
            {
                return TypedResults.Problem(
                    detail: $"Cannot assign permission '{permission}' — caller does not possess it.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        ApiKeyGenerationResult keyResult = generator.Generate(request.Type, request.Environment);

        var entry = ApiKeyEntry.Create(
            guidGenerator.Create(),
            request.Name,
            request.Type,
            request.Environment,
            keyResult.HashedKey,
            keyResult.Prefix,
            keyResult.LastFourChars);
        entry.UpdatePermissions(request.Permissions ?? []);
        entry.UpdateAllowedCidrs(request.AllowedCidrs ?? []);
        entry.SetExpiration(request.ExpiresAt);
        entry.SetCacheBehavior(request.CacheBehavior);

        await adminStore.CreateAsync(entry, cancellationToken).ConfigureAwait(false);

        var response = new ApiKeyCreateResponse(
            entry.Id,
            keyResult.RawSecret,
            keyResult.Prefix,
            keyResult.LastFourChars,
            entry.Name,
            entry.Type,
            entry.Environment,
            entry.ExpiresAt);

        return TypedResults.Created($"/{entry.Id}", response);
    }
}
