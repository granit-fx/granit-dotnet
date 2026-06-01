using Granit.Guids;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Hostnames.Endpoints.Options;
using Granit.Hostnames.Endpoints.Permissions;
using Granit.Hostnames.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

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

        group.MapPost("/{id:guid}/verify-now", HandleVerifyNowAsync)
            .RequireAuthorization(HostnamesPermissions.Hostnames.Manage)
            .WithName("VerifyHostnameNow")
            .WithSummary("Manually triggers DNS verification for a hostname.")
            .WithDescription("For Verifying, Error, or Active hostnames: resets exponential backoff and re-queues the DNS check (transitions to Verifying). For Pending hostnames: initializes verification by minting the challenge token and expected DNS records, then transitions to Verifying. Returns 409 when the hostname is Pending and the platform ingress target is not configured. Returns 202 Accepted with the updated hostname record. Returns 404 when not found. Requires the Hostnames.Manage permission.")
            .Produces<ManagedHostnameResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/certificate-status", HandleReportCertificateStatusAsync)
            .RequireAuthorization(HostnamesPermissions.Certificates.Report)
            .WithName("ReportHostnameCertificateStatus")
            .WithSummary("Reports the SSL/TLS certificate status from the edge provider.")
            .WithDescription("Webhook endpoint called by the edge provider (e.g. Cloudflare, AWS) when the certificate state changes. Stores the new CertificateStatus and expiry date, and dispatches the appropriate integration event (HostnameCertificateSecuredEto or HostnameCertificateFailedEto). Returns 204 on success, 404 when not found. Requires the Hostnames.Certificates.Report permission.")
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
        [FromServices] IOptions<HostnamesOptions> hostnamesOptions,
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

        // When the platform ingress target is configured, mint the DNS challenge immediately
        // so the API response already carries the records the customer must configure.
        HostnamesOptions opts = hostnamesOptions.Value;
        if (!string.IsNullOrEmpty(opts.IngressTarget))
        {
            string token = guidGenerator.Create().ToString("N");
            hostname.BeginVerification(token, BuildExpectedRecords(hostname.Host.Value, token, opts));
        }

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

    private static async Task<Results<Accepted<ManagedHostnameResponse>, ProblemHttpResult>> HandleVerifyNowAsync(
        Guid id,
        [FromServices] IManagedHostnameReader reader,
        [FromServices] IManagedHostnameWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IOptions<HostnamesOptions> hostnamesOptions,
        CancellationToken cancellationToken)
    {
        ManagedHostname? hostname = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (hostname is null)
        {
            return HostnameNotFound(id);
        }

        if (hostname.Status == HostnameStatus.Pending)
        {
            HostnamesOptions opts = hostnamesOptions.Value;
            if (string.IsNullOrEmpty(opts.IngressTarget))
            {
                return TypedResults.Problem(
                    detail: "Hostname verification has not been initialized. Configure 'Hostnames:IngressTarget' or begin verification manually.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            string token = guidGenerator.Create().ToString("N");
            hostname.BeginVerification(token, BuildExpectedRecords(hostname.Host.Value, token, opts));
        }
        else
        {
            hostname.RequestRecheck();
        }

        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.Accepted((string?)null, MapToResponse(hostname));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleReportCertificateStatusAsync(
        Guid id,
        ReportCertificateStatusRequest body,
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

        hostname.ReportCertificateStatus(body.Status, body.ExpiresAt);
        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<ExpectedDnsRecord> BuildExpectedRecords(
        string host, string token, HostnamesOptions opts) =>
    [
        new(DnsRecordType.Cname, host, opts.IngressTarget!),
        new(DnsRecordType.Txt, $"{opts.TxtChallengePrefix}.{host}", $"granit-verify={token}"),
    ];

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
            h.VerificationToken,
            h.ExpectedDnsRecords,
            h.LastCheckedAt,
            h.Conflicts,
            h.FailedCheckCount,
            h.NextCheckAt,
            h.CertificateStatus.ToString(),
            h.CertExpiresAt,
            h.CreatedAt,
            h.CreatedBy,
            h.ModifiedAt,
            h.ModifiedBy);
}
