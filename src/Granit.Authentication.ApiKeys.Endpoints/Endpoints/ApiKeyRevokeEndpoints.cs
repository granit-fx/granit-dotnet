using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authentication.ApiKeys.Endpoints.Endpoints;

/// <summary>
/// Endpoint for revoking API keys.
/// </summary>
internal static class ApiKeyRevokeEndpoints
{
    internal static RouteGroupBuilder MapRevokeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/revoke", RevokeAsync)
            .WithName("RevokeApiKey")
            .WithSummary("Revokes an API key. The key will no longer be accepted for authentication.")
            .WithDescription("Permanently revokes the API key. Any subsequent authentication attempt using this key will be rejected. This operation is irreversible — use rotate instead if you need a replacement key. Returns 404 if the key does not exist.");

        return group;
    }

    private static async Task<Results<NoContent, NotFound>> RevokeAsync(
        Guid id,
        [FromServices] IApiKeyAdminStore adminStore,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        bool revoked = await adminStore.RevokeAsync(id, clock.Now, cancellationToken)
            .ConfigureAwait(false);

        if (!revoked)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
