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

    /// <summary>Records that a provider prepared a fragment ready for assembly.</summary>
    Task WriteFragmentPreparedAsync(PrivacyExportFragmentPreparedAudit data, CancellationToken cancellationToken);

    /// <summary>Records the start of the archive-assembly job — emitted before the
    /// first shard streams so an investigator can see "the assembler picked it up"
    /// distinct from "the assembler finished it".</summary>
    Task WriteAssemblyStartedAsync(PrivacyExportAssemblyStartedAudit data, CancellationToken cancellationToken);

    /// <summary>Records that a single shard finished its multipart upload — emitted
    /// after each shard's <c>CompleteMultipartUpload</c> so the audit trail captures
    /// the per-shard sha256 + duration without waiting for the whole assembly to land.</summary>
    Task WriteShardCompletedAsync(PrivacyExportShardCompletedAudit data, CancellationToken cancellationToken);

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
/// <param name="DownloaderUserId">User who actually issued the download — typically
/// equal to <paramref name="SubjectUserId"/> for self-service, but distinct when
/// the export was triggered via <c>POST /privacy/exports/on-behalf-of</c> and the
/// admin retrieves a manifest.</param>
/// <param name="SubjectUserId">Data subject the archive belongs to (from the
/// tracker row). Captured separately so ROPA can reconstruct subject vs.
/// downloader without a cross-table join.</param>
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
    Guid DownloaderUserId,
    Guid SubjectUserId,
    Guid? TenantId,
    int ShardIndex,
    string? ClientIp,
    string? UserAgent,
    string? AuthMethod,
    string? CorrelationId,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit payload for a provider fragment that completed its upload step.
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="SubjectUserId">Data subject the fragment belongs to.</param>
/// <param name="TenantId">Tenant context.</param>
/// <param name="ProviderName">Originating <c>IPrivacyDataProvider.ProviderName</c>.</param>
/// <param name="FragmentKind">Fragment kind — <c>"staged"</c>, <c>"passthrough"</c>, or
/// <c>"empty"</c> (sentinel for providers with no data).</param>
/// <param name="EntryPathHash">SHA-256 hex digest of the entry path. The raw path
/// can encode subject-visible filenames; the hash gives an investigator something
/// to correlate by without leaking the original string into the audit row.</param>
/// <param name="SizeBytes">Declared / measured payload size where known,
/// <see langword="null"/> for empty-sentinel fragments.</param>
/// <param name="Timestamp">When the fragment finished uploading (UTC).</param>
public sealed record PrivacyExportFragmentPreparedAudit(
    Guid RequestId,
    Guid SubjectUserId,
    Guid? TenantId,
    string ProviderName,
    string FragmentKind,
    string EntryPathHash,
    long? SizeBytes,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit payload for the moment the assembly background job picks the request up
/// — distinct from <see cref="PrivacyExportRequestedAudit"/> (which records the
/// HTTP request) and <see cref="PrivacyExportCompletedAudit"/> (which records the
/// terminal state).
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="SubjectUserId">Data subject.</param>
/// <param name="TenantId">Tenant context.</param>
/// <param name="Regulation">Regulation code.</param>
/// <param name="ExpectedFragmentCount">Fragments the saga collected from providers —
/// the assembler iterates over these.</param>
/// <param name="IsResumed"><see langword="true"/> when the assembler picked up a
/// prior crashed run via the checkpoint store.</param>
/// <param name="Timestamp">Assembly start timestamp (UTC).</param>
public sealed record PrivacyExportAssemblyStartedAudit(
    Guid RequestId,
    Guid SubjectUserId,
    Guid? TenantId,
    string Regulation,
    int ExpectedFragmentCount,
    bool IsResumed,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit payload for a single shard's multipart upload completion. Emitted as soon
/// as <c>CompleteMultipartUpload</c> returns, before subsequent shards or the
/// manifest, so the per-shard sha256 + duration is on the audit trail even if a
/// later shard or the manifest write fails.
/// </summary>
/// <param name="RequestId">Saga / tracker correlation id.</param>
/// <param name="SubjectUserId">Data subject.</param>
/// <param name="TenantId">Tenant context.</param>
/// <param name="ShardIndex">Zero-based shard index in write order.</param>
/// <param name="SizeBytes">Total bytes the multipart upload committed.</param>
/// <param name="Sha256Hex">Lowercase hex SHA-256 digest of the shard's bytes
/// (matches the manifest's per-shard sha256).</param>
/// <param name="DurationMs">Wall-clock time from the shard's open to its
/// <c>CompleteMultipartUpload</c> return.</param>
/// <param name="Timestamp">Completion timestamp (UTC).</param>
public sealed record PrivacyExportShardCompletedAudit(
    Guid RequestId,
    Guid SubjectUserId,
    Guid? TenantId,
    int ShardIndex,
    long SizeBytes,
    string Sha256Hex,
    long DurationMs,
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
