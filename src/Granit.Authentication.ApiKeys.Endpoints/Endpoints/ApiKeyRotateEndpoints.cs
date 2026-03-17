using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Guids;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authentication.ApiKeys.Endpoints.Endpoints;

/// <summary>
/// Endpoint for rotating API keys (revoke old + create new with same settings).
/// </summary>
internal static class ApiKeyRotateEndpoints
{
    internal static RouteGroupBuilder MapRotateEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/rotate", RotateAsync)
            .WithName("RotateApiKey")
            .WithSummary("Rotates an API key: revokes the current key and creates a replacement with the same settings.")
            .WithDescription("Atomically revokes the existing key and creates a new one inheriting the same name, type, environment, permissions, CIDR restrictions, and expiration. The response contains the new raw secret (shown once) and both the old and new key IDs. Returns 404 if the key does not exist or is already revoked.");

        return group;
    }

    private static async Task<Results<Ok<ApiKeyRotateResponse>, NotFound>> RotateAsync(
        Guid id,
        IApiKeyAdminStore adminStore,
        IApiKeyGenerator generator,
        IGuidGenerator guidGenerator,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ApiKeyEntry? existing = await adminStore.FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || existing.RevokedAt.HasValue)
        {
            return TypedResults.NotFound();
        }

        // Revoke old key
        await adminStore.RevokeAsync(id, clock.Now, cancellationToken).ConfigureAwait(false);

        // Generate new key with same settings
        ApiKeyGenerationResult keyResult = generator.Generate(existing.Type, existing.Environment);

        var newEntry = new ApiKeyEntry
        {
            Id = guidGenerator.Create(),
            Name = existing.Name,
            Type = existing.Type,
            Environment = existing.Environment,
            HashedKey = keyResult.HashedKey,
            Prefix = keyResult.Prefix,
            LastFourChars = keyResult.LastFourChars,
            Permissions = [.. existing.Permissions],
            AllowedCidrs = [.. existing.AllowedCidrs],
            ExpiresAt = existing.ExpiresAt,
            CacheBehavior = existing.CacheBehavior,
            TenantId = existing.TenantId,
            CreatedAt = clock.Now,
        };

        await adminStore.CreateAsync(newEntry, cancellationToken).ConfigureAwait(false);

        var response = new ApiKeyRotateResponse(
            newEntry.Id,
            keyResult.RawSecret,
            keyResult.Prefix,
            keyResult.LastFourChars,
            id);

        return TypedResults.Ok(response);
    }
}
