namespace Granit.Privacy.DataExport.Audit;

/// <summary>
/// Records the personal-data-export lifecycle to the host's audit trail for
/// ROPA (GDPR Art. 30) and ISO 27001 A.5.34 evidence. The privacy module is
/// deliberately independent of <c>Granit.Auditing</c> — hosts that don't ship
/// an audit trail get the no-op default, hosts that do reference the bridge
/// package <c>Granit.Privacy.Auditing</c> which wires the writes to
/// <c>IAuditingWriter</c> from inside <c>Granit.Auditing</c>.
/// </summary>
/// <remarks>
/// Payloads are intentionally narrow — only what an investigator needs to
/// reconstruct the request without storing the personal data itself. No
/// <c>EntryPath</c>, no <c>BlobReference</c>, and the IP address is expected
/// to be pseudonymised at the call site (the same way the consent endpoint
/// already does).
/// </remarks>
public interface IPrivacyExportAuditWriter
{
    /// <summary>Records that a data subject filed an export request.</summary>
    Task WriteExportRequestedAsync(PrivacyExportRequestedAudit data, CancellationToken cancellationToken);

    /// <summary>Records that the archive assembly job persisted every shard plus the manifest.</summary>
    Task WriteExportCompletedAsync(PrivacyExportCompletedAudit data, CancellationToken cancellationToken);

    /// <summary>Records a download against a specific shard. Triggers the
    /// "first-download" notification when paired with a host-side dedup table.</summary>
    Task WriteShardDownloadedAsync(PrivacyExportShardDownloadedAudit data, CancellationToken cancellationToken);

    /// <summary>Records a failed assembly run after the Wolverine retry budget is exhausted.
    /// Excludes <c>EntryPath</c> / <c>BlobReference</c> — the failure type and retry count
    /// are enough for triage, the rest belongs in correlated logs.</summary>
    Task WriteExportFailedAsync(PrivacyExportFailedAudit data, CancellationToken cancellationToken);
}

/// <summary>
/// Audit payload for the export-request creation step (POST /privacy/exports).
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="CallerUserId">User who filed the request — typically equal to
/// <paramref name="SubjectUserId"/> for self-service.</param>
/// <param name="SubjectUserId">Data subject the export is about.</param>
/// <param name="TenantId">Tenant context — propagates to the audit entry.</param>
/// <param name="Regulation">Regulation code the request was filed under (e.g. <c>EU_GDPR</c>).</param>
/// <param name="ResolvedScopes">Visible provider scopes resolved at request time —
/// the slice the saga actually fans out to, after gates.</param>
/// <param name="ClientIp">Pseudonymised client IP (call site responsible for masking).</param>
/// <param name="UserAgent">Client user-agent string.</param>
/// <param name="CorrelationId">Distributed-tracing correlation id for cross-store lookups.</param>
/// <param name="Timestamp">When the request was accepted (UTC).</param>
public sealed record PrivacyExportRequestedAudit(
    Guid RequestId,
    Guid CallerUserId,
    Guid SubjectUserId,
    Guid? TenantId,
    string Regulation,
    IReadOnlyList<string> ResolvedScopes,
    string? ClientIp,
    string? UserAgent,
    string? CorrelationId,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit payload for the export-completion step (every shard + manifest persisted).
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="SubjectUserId">Data subject the export belongs to.</param>
/// <param name="TenantId">Tenant context.</param>
/// <param name="Regulation">Regulation code.</param>
/// <param name="ShardCount">Number of shard ZIPs produced.</param>
/// <param name="IsPartial"><c>true</c> when the saga timed out before some providers
/// responded (the user receives the failure email instead of the ready one).</param>
/// <param name="AssemblyDurationMs">End-to-end assembly job duration.</param>
/// <param name="Timestamp">Completion timestamp (UTC).</param>
public sealed record PrivacyExportCompletedAudit(
    Guid RequestId,
    Guid SubjectUserId,
    Guid? TenantId,
    string Regulation,
    int ShardCount,
    bool IsPartial,
    long AssemblyDurationMs,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit payload for a single shard download from the BFF endpoint.
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="SubjectUserId">Data subject who downloaded their shard.</param>
/// <param name="TenantId">Tenant context.</param>
/// <param name="ShardIndex">Zero-based shard index served. <c>-1</c> for the manifest download.</param>
/// <param name="ClientIp">Pseudonymised client IP.</param>
/// <param name="UserAgent">Client user-agent string.</param>
/// <param name="AuthMethod">Authentication method used by the BFF for the request
/// (typically <c>"oidc"</c>; future channels like FIDO2 token replace this).</param>
/// <param name="CorrelationId">Distributed-tracing correlation id.</param>
/// <param name="Timestamp">Download timestamp (UTC).</param>
public sealed record PrivacyExportShardDownloadedAudit(
    Guid RequestId,
    Guid SubjectUserId,
    Guid? TenantId,
    int ShardIndex,
    string? ClientIp,
    string? UserAgent,
    string? AuthMethod,
    string? CorrelationId,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit payload for a terminal assembly failure (DLQ — retry budget exhausted).
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="SubjectUserId">Data subject — kept so the host can scrub on a
/// future deletion request.</param>
/// <param name="TenantId">Tenant context.</param>
/// <param name="ExceptionType">Short name of the failure type (no message, no stack).</param>
/// <param name="RetryCount">How many times Wolverine re-dispatched before giving up.</param>
/// <param name="Timestamp">Failure timestamp (UTC).</param>
public sealed record PrivacyExportFailedAudit(
    Guid RequestId,
    Guid SubjectUserId,
    Guid? TenantId,
    string ExceptionType,
    int RetryCount,
    DateTimeOffset Timestamp);
