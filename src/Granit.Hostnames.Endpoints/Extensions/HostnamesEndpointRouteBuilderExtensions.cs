using Granit.Guids;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Hostnames.Endpoints.Options;
using Granit.Hostnames.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Hostnames.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping managed hostname endpoints.
/// </summary>
public static class HostnamesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps managed hostname endpoints under <c>/{prefix}</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customise <see cref="HostnamesEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitHostnames(
        this IEndpointRouteBuilder endpoints,
        Action<HostnamesEndpointsOptions>? configure = null)
    {
        HostnamesEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        MapReadEndpoints(group);
        MapWriteEndpoints(group);

        return group;
    }

    // ── Read endpoints ────────────────────────────────────────────────────────

    private static void MapReadEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/", HandleListByOwnerAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Read)
            .WithName("ListManagedHostnames")
            .WithSummary("Lists the hostnames registered for an owning resource.")
            .WithDescription("Returns all hostnames registered for the specified owner (ownerType + ownerId pair), ordered by host name. An empty list is returned when the owner has no registered hostnames. Requires the Hostnames.Read permission.")
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
    }

    // ── Write endpoints ───────────────────────────────────────────────────────

    private static void MapWriteEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Manage)
            .WithName("CreateManagedHostname")
            .WithSummary("Registers a new managed hostname.")
            .WithDescription("Registers a fully-qualified hostname against an owning resource. The hostname must be globally unique — a second registration for the same host, regardless of owner, is rejected with 409. Returns the created hostname record with a 201 status. Requires the Hostnames.Manage permission.")
            .Produces<ManagedHostnameResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", HandleDeleteAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Manage)
            .WithName("DeleteManagedHostname")
            .WithSummary("Deletes a managed hostname.")
            .WithDescription("Removes a hostname registration, freeing the host for re-registration by any owner. Returns 204 on success, 404 when not found. Requires the Hostnames.Manage permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/primary", HandleSetPrimaryAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Manage)
            .WithName("SetPrimaryHostname")
            .WithSummary("Marks a hostname as the owner's canonical hostname.")
            .WithDescription("Sets the IsPrimary flag on the specified hostname. This does not automatically clear the flag on other hostnames owned by the same resource — manage that explicitly. Returns 204 on success, 404 when not found. Requires the Hostnames.Manage permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/primary", HandleClearPrimaryAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Manage)
            .WithName("ClearPrimaryHostname")
            .WithSummary("Clears the canonical hostname flag.")
            .WithDescription("Removes the IsPrimary flag from the specified hostname. Returns 204 on success, 404 when not found. Requires the Hostnames.Manage permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<Ok<IReadOnlyList<ManagedHostnameResponse>>> HandleListByOwnerAsync(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        [FromServices] IManagedHostnameReader reader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ManagedHostname> hostnames = await reader
            .ListByOwnerAsync(ownerType, ownerId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<ManagedHostnameResponse>>(
            hostnames.Select(MapToResponse).ToList());
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
            ? HostnameNotFound(id)
            : TypedResults.Ok(MapToResponse(hostname));
    }

    private static async Task<Results<Created<ManagedHostnameResponse>, ProblemHttpResult, ValidationProblem>> HandleCreateAsync(
        CreateManagedHostnameRequest body,
        [FromServices] IManagedHostnameWriter writer,
        [FromServices] IManagedHostnameReader reader,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Hostname hostnameValue;
        try
        {
            hostnameValue = Hostname.Create(body.Host);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        ManagedHostname? existing = await reader
            .FindByHostAsync(hostnameValue.Value, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return TypedResults.Problem(
                detail: $"The hostname '{hostnameValue.Value}' is already registered.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var hostname = ManagedHostname.Create(
            guidGenerator.Create(),
            hostnameValue,
            body.OwnerType,
            body.OwnerId,
            body.TenantId,
            body.IsPrimary);

        await writer.AddAsync(hostname, cancellationToken).ConfigureAwait(false);

        ManagedHostnameResponse response = MapToResponse(hostname);
        return TypedResults.Created($"/{response.Id}", response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteAsync(
        Guid id,
        [FromServices] IManagedHostnameReader reader,
        [FromServices] IManagedHostnameWriter writer,
        CancellationToken cancellationToken)
    {
        ManagedHostname? hostname = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (hostname is null)
        {
            return HostnameNotFound(id);
        }

        await writer.DeleteAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleSetPrimaryAsync(
        Guid id,
        [FromServices] IManagedHostnameReader reader,
        [FromServices] IManagedHostnameWriter writer,
        CancellationToken cancellationToken)
    {
        ManagedHostname? hostname = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (hostname is null)
        {
            return HostnameNotFound(id);
        }

        hostname.SetPrimary();
        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleClearPrimaryAsync(
        Guid id,
        [FromServices] IManagedHostnameReader reader,
        [FromServices] IManagedHostnameWriter writer,
        CancellationToken cancellationToken)
    {
        ManagedHostname? hostname = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (hostname is null)
        {
            return HostnameNotFound(id);
        }

        hostname.ClearPrimary();
        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static ProblemHttpResult HostnameNotFound(Guid id) =>
        TypedResults.Problem(
            detail: $"No managed hostname with id '{id}' was found.",
            statusCode: StatusCodes.Status404NotFound);

    internal static ManagedHostnameResponse MapToResponse(ManagedHostname h) =>
        new(h.Id,
            h.Host.Value,
            h.OwnerType,
            h.OwnerId,
            h.TenantId,
            h.IsPrimary,
            h.Status.ToString(),
            h.CreatedAt,
            h.CreatedBy,
            h.ModifiedAt,
            h.ModifiedBy);
}
