using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Hostnames.Endpoints.Internal;
using Granit.Hostnames.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Hostnames.Endpoints.Endpoints;

internal static class HostnamesReadEndpoints
{
    internal static RouteGroupBuilder MapHostnamesReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleListByOwnerAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Read)
            .WithName("ListManagedHostnames")
            .WithSummary("Lists the hostnames registered for an owning resource.")
            .WithDescription("Returns hostnames registered for the specified owner (ownerType + ownerId pair), ordered by host name. At most maxResults entries are returned (capped at 500). An empty list is returned when the owner has no registered hostnames. Requires the Hostnames.Read permission.")
            .Produces<IReadOnlyList<ManagedHostnameResponse>>();

        group.MapGet("/availability", HandleAvailabilityAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Read)
            .WithName("CheckHostnameAvailability")
            .WithSummary("Checks whether a hostname is available for registration.")
            .WithDescription("Pre-flight check: returns whether the given fully-qualified hostname is free. A hostname is unavailable when it is already registered by any owner (globally unique constraint). Use before CreateManagedHostname to give immediate feedback. Requires the Hostnames.Read permission.")
            .Produces<HostnameAvailabilityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", HandleGetByIdAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Read)
            .WithName("GetManagedHostname")
            .WithSummary("Returns a managed hostname by id.")
            .WithDescription("Returns the full hostname record for the given id. Returns 404 when no hostname with that id exists. Requires the Hostnames.Read permission.")
            .Produces<ManagedHostnameResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private const int DefaultMaxResults = 100;
    private const int AbsoluteMaxResults = 500;

    private static async Task<Ok<IReadOnlyList<ManagedHostnameResponse>>> HandleListByOwnerAsync(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        [FromServices] IManagedHostnameReader reader,
        CancellationToken cancellationToken,
        [FromQuery] int maxResults = DefaultMaxResults)
    {
        int capped = Math.Clamp(maxResults, 1, AbsoluteMaxResults);

        IReadOnlyList<ManagedHostname> hostnames = await reader
            .ListByOwnerAsync(ownerType, ownerId, capped, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<ManagedHostnameResponse>>(
            hostnames.Select(HostnamesResponseMapper.ToResponse).ToList());
    }

    private static async Task<Results<Ok<HostnameAvailabilityResponse>, ProblemHttpResult>> HandleAvailabilityAsync(
        [FromQuery] string host,
        [FromServices] IManagedHostnameReader reader,
        CancellationToken cancellationToken)
    {
        Hostname hostnameValue;
        try
        {
            hostnameValue = Hostname.Create(host);
        }
        catch (ArgumentException)
        {
            return TypedResults.Problem(
                detail: $"'{host}' is not a valid fully-qualified domain name.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ManagedHostname? existing = await reader
            .FindByHostAsync(hostnameValue.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(
            new HostnameAvailabilityResponse(hostnameValue.Value, IsAvailable: existing is null));
    }

    private static async Task<Results<Ok<ManagedHostnameResponse>, ProblemHttpResult>> HandleGetByIdAsync(
        Guid id,
        [FromServices] IManagedHostnameReader reader,
        CancellationToken cancellationToken)
    {
        ManagedHostname? hostname = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return hostname is null
            ? HostnamesResponseMapper.HostnameNotFound(id)
            : TypedResults.Ok(HostnamesResponseMapper.ToResponse(hostname));
    }
}
