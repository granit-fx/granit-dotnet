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

internal static class HostnamesWriteEndpoints
{
    internal static RouteGroupBuilder MapHostnamesWriteEndpoints(this RouteGroupBuilder group)
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

        return group;
    }

    private static async Task<Results<Created<ManagedHostnameResponse>, ProblemHttpResult, ValidationProblem>> HandleCreateAsync(
        CreateManagedHostnameRequest body,
        [FromServices] IHostnameRegistrationService service,
        CancellationToken cancellationToken)
    {
        // Validate FQDN format here so the invalid-host path returns 400, not 500.
        try { Hostname.Create(body.Host); }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        HostnameRegistrationResult result = await service
            .RegisterAsync(body.Host, body.OwnerType, body.OwnerId, body.TenantId, body.IsPrimary, cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome switch
        {
            HostnameRegistrationOutcome.Succeeded =>
                TypedResults.Created($"/{result.Hostname!.Id}", HostnamesResponseMapper.ToResponse(result.Hostname)),
            HostnameRegistrationOutcome.HostAlreadyTaken =>
                TypedResults.Problem(
                    detail: $"The hostname '{body.Host}' is already registered.",
                    statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unexpected outcome: {result.Outcome}"),
        };
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
            return HostnamesResponseMapper.HostnameNotFound(id);
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
            return HostnamesResponseMapper.HostnameNotFound(id);
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
            return HostnamesResponseMapper.HostnameNotFound(id);
        }

        hostname.ClearPrimary();
        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Accepted<ManagedHostnameResponse>, ProblemHttpResult>> HandleVerifyNowAsync(
        Guid id,
        [FromServices] IHostnameRegistrationService service,
        CancellationToken cancellationToken)
    {
        RequestVerificationResult result = await service
            .RequestVerificationAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome switch
        {
            RequestVerificationOutcome.Succeeded =>
                TypedResults.Accepted((string?)null, HostnamesResponseMapper.ToResponse(result.Hostname!)),
            RequestVerificationOutcome.NotFound =>
                HostnamesResponseMapper.HostnameNotFound(id),
            RequestVerificationOutcome.IngressNotConfigured =>
                TypedResults.Problem(
                    detail: "Hostname verification has not been initialized. Configure 'Hostnames:IngressTarget' or begin verification manually.",
                    statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unexpected outcome: {result.Outcome}"),
        };
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
            return HostnamesResponseMapper.HostnameNotFound(id);
        }

        hostname.ReportCertificateStatus(body.Status, body.ExpiresAt);
        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

}
