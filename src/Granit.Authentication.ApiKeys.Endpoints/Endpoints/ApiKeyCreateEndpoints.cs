using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Guids;
using Granit.Timing;
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
            .WithDescription("Generates a new API key with the specified type, environment, permissions, and optional CIDR restrictions. The response includes the full raw secret — this is the only time the secret is available. Store it securely; it cannot be retrieved later. The key is immediately active.");

        return group;
    }

    private static async Task<Created<ApiKeyCreateResponse>> CreateAsync(
        ApiKeyCreateRequest request,
        [FromServices] IApiKeyGenerator generator,
        [FromServices] IApiKeyAdminStore adminStore,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        ApiKeyGenerationResult keyResult = generator.Generate(request.Type, request.Environment);

        var entry = new ApiKeyEntry
        {
            Id = guidGenerator.Create(),
            Name = request.Name,
            Type = request.Type,
            Environment = request.Environment,
            HashedKey = keyResult.HashedKey,
            Prefix = keyResult.Prefix,
            LastFourChars = keyResult.LastFourChars,
            Permissions = request.Permissions ?? [],
            AllowedCidrs = request.AllowedCidrs ?? [],
            ExpiresAt = request.ExpiresAt,
            CacheBehavior = request.CacheBehavior,
            CreatedAt = clock.Now,
        };

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
