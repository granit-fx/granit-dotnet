using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Authentication.ApiKeys.Events;
using Granit.Events;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
            .WithDescription("Atomically revokes the existing key and creates a new one inheriting the same name, type, environment, permissions, CIDR restrictions, and expiration. The response contains the new raw secret (shown once) and both the old and new key IDs. Returns 404 if the key does not exist or is already revoked.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<ApiKeyRotateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<ApiKeyRotateResponse>, NotFound>> RotateAsync(
        Guid id,
        [FromServices] IApiKeyAdminStore adminStore,
        [FromServices] IApiKeyGenerator generator,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        [FromServices] IDistributedEventBus eventBus,
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

        var newEntry = ApiKeyEntry.Create(
            guidGenerator.Create(),
            existing.Name,
            existing.Type,
            existing.Environment,
            keyResult.HashedKey,
            keyResult.Prefix,
            keyResult.LastFourChars,
            existing.TenantId);
        newEntry.UpdatePermissions([.. existing.Permissions]);
        newEntry.UpdateAllowedCidrs([.. existing.AllowedCidrs]);
        newEntry.SetExpiration(existing.ExpiresAt);
        newEntry.SetCacheBehavior(existing.CacheBehavior);

        await adminStore.CreateAsync(newEntry, cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new ApiKeyRotatedEto(id, newEntry.Id, keyResult.HashedKey),
            cancellationToken).ConfigureAwait(false);

        var response = new ApiKeyRotateResponse(
            newEntry.Id,
            keyResult.RawSecret,
            keyResult.Prefix,
            keyResult.LastFourChars,
            id);

        return TypedResults.Ok(response);
    }
}
