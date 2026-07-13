namespace Granit.Privacy.DataExport;

/// <summary>
/// Application service that orchestrates personal-data export requests (GDPR Art. 15/20):
/// optional tenant-bound subject validation, scope normalization, the tracker write, the
/// distributed-event dispatch, metrics, and the ROPA audit entry. Collapses the near-identical
/// self-service and admin "on behalf of" HTTP handlers into one call and keeps the workflow in
/// the domain layer, unit-testable in isolation.
/// </summary>
public interface IPrivacyExportRequestService
{
    /// <summary>Files an export request and returns its correlation id and acceptance timestamp.</summary>
    Task<RequestExportOutcome> RequestExportAsync(
        RequestExportCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Input for <see cref="IPrivacyExportRequestService.RequestExportAsync"/>.</summary>
/// <param name="SubjectUserId">Data subject the export targets.</param>
/// <param name="CallerUserId">User who filed the request — equal to <paramref name="SubjectUserId"/>
/// for self-service, distinct for an admin DSR.</param>
/// <param name="Scopes">Requested provider scopes, or <c>null</c> / empty for "everything visible".</param>
/// <param name="Regulation">Resolved regulation code, resolved at the call site.</param>
/// <param name="ValidateSubject">When <c>true</c> the subject is checked against the caller's
/// tenant before the request is accepted (admin DSR path); a missing subject yields
/// <see cref="RequestExportResult.SubjectNotFound"/>.</param>
/// <param name="Audit">HTTP-layer audit metadata (already-pseudonymized IP, user-agent, correlation id).</param>
public sealed record RequestExportCommand(
    Guid SubjectUserId,
    Guid CallerUserId,
    IReadOnlyList<string>? Scopes,
    string Regulation,
    bool ValidateSubject,
    ExportRequestAuditMetadata Audit);

/// <summary>HTTP-layer metadata captured for the ROPA audit row; the call site masks the IP.</summary>
/// <param name="ClientIp">Pseudonymized client IP.</param>
/// <param name="UserAgent">Client user-agent string.</param>
/// <param name="CorrelationId">Distributed-tracing correlation id.</param>
public sealed record ExportRequestAuditMetadata(
    string? ClientIp,
    string? UserAgent,
    string? CorrelationId);

/// <summary>Discriminates the outcome of an export request.</summary>
public enum RequestExportResult
{
    /// <summary>The request was accepted and the saga was triggered.</summary>
    Accepted,

    /// <summary>Subject validation was requested but the subject is not visible in the caller's tenant.</summary>
    SubjectNotFound,
}

/// <summary>Result of <see cref="IPrivacyExportRequestService.RequestExportAsync"/>.</summary>
/// <param name="Result">What happened.</param>
/// <param name="RequestId">Correlation id of the created request (unset for <see cref="RequestExportResult.SubjectNotFound"/>).</param>
/// <param name="RequestedAt">Acceptance timestamp (UTC).</param>
public sealed record RequestExportOutcome(
    RequestExportResult Result,
    Guid RequestId,
    DateTimeOffset RequestedAt);
