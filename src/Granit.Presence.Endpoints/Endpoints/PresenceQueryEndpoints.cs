using Granit.Presence.Abstractions;
using Granit.Presence.Domain;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Presence.Endpoints.Endpoints;

internal static class PresenceQueryEndpoints
{
    public static RouteGroupBuilder MapQueryEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/users/{userId:guid}", GetUserPresenceAsync)
            .WithName("GetUserPresence")
            .WithSummary("Returns the presence snapshot of one user.")
            .WithDescription("Computes the effective presence status for the specified user. Always returns 200 OK — unknown or never-seen users are reported as Offline with a null LastSeenUtc rather than 404, so clients can use this endpoint to refresh a roster without branching on missing rows.")
            .Produces<PresenceResponse>();

        group.MapPost("/users/batch", GetBatchPresenceAsync)
            .WithName("GetBatchPresence")
            .WithSummary("Returns presence snapshots for a batch of users.")
            .WithDescription("Returns a dictionary keyed by user id. POST is used because UUID lists exceed practical URL length around 50 entries. Cache via React Query / SWR client-side; the server response carries an ETag for conditional re-fetching.")
            .Produces<BatchPresenceResponse>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Ok<PresenceResponse>> GetUserPresenceAsync(
        Guid userId,
        [FromServices] IPresenceQueryService queryService,
        CancellationToken cancellationToken)
    {
        PresenceSnapshot snapshot = await queryService.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(PresenceResponseMapper.ToResponse(snapshot));
    }

    private static async Task<Ok<BatchPresenceResponse>> GetBatchPresenceAsync(
        BatchPresenceRequest request,
        [FromServices] IPresenceQueryService queryService,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, PresenceSnapshot> snapshots = await queryService
            .GetManyAsync(request.UserIds, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PresenceResponse> responses = new(snapshots.Count);
        foreach ((Guid userId, PresenceSnapshot snapshot) in snapshots)
        {
            responses[userId] = PresenceResponseMapper.ToResponse(snapshot);
        }

        return TypedResults.Ok(new BatchPresenceResponse(responses));
    }
}
