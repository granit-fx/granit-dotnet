using Granit.Domain;
using Granit.Hostnames.Domain.Events;
using Granit.Workflow;
using Granit.Workflow.Domain;
using Granit.Workflow.Events;

namespace Granit.Hostnames.Domain;

/// <summary>
/// A hostname registered against an owning resource, for host-based routing. The owner is opaque —
/// <see cref="OwnerType"/> + <see cref="OwnerId"/> — so any consumer (CMS site, API gateway, billing
/// portal, …) can claim a hostname without this capability depending on it. <see cref="Host"/> is
/// globally unique, which gives anti-hijacking for free (a second owner cannot claim a taken host).
/// </summary>
/// <remarks>
/// Implements <see cref="IWorkflowStateful"/> so the <c>WorkflowTransitionInterceptor</c> records
/// every state transition in an immutable audit trail (ISO 27001). Transitions are system-driven
/// (the DNS verifier); no human approval step is needed on the verification path.
/// </remarks>
public sealed class ManagedHostname : AuditedAggregateRoot, IMultiTenant, IConcurrencyAware, IWorkflowStateful
{
    /// <summary>Capped exponential backoff table (indexed by consecutive failure count, 1-based).</summary>
    private static readonly TimeSpan[] BackoffTable =
    [
        TimeSpan.FromMinutes(1),   // 1st failure
        TimeSpan.FromMinutes(5),   // 2nd
        TimeSpan.FromMinutes(15),  // 3rd
        TimeSpan.FromHours(1),     // 4th
        TimeSpan.FromHours(6),     // 5th
        TimeSpan.FromHours(24),    // 6th and beyond (cap)
    ];

    /// <summary>
    /// After this many consecutive failures the poller stops picking up the domain automatically
    /// (manual "verify now" required to re-enter the Verifying state).
    /// </summary>
    public const int DormancyThreshold = 20;

    /// <summary>
    /// Allowed workflow transitions for this aggregate (Pending → Verifying → Active/Error, retry, re-check).
    /// </summary>
    public static readonly WorkflowDefinition<HostnameStatus> WorkflowDefinition =
        WorkflowDefinition<HostnameStatus>.Create(b => b
            .InitialState(HostnameStatus.Pending)
            .Transition(HostnameStatus.Pending, HostnameStatus.Verifying,
                t => t.Named("begin-verification"))
            .Transition(HostnameStatus.Verifying, HostnameStatus.Active,
                t => t.Named("mark-verified"))
            .Transition(HostnameStatus.Verifying, HostnameStatus.Error,
                t => t.Named("mark-failed"))
            .Transition(HostnameStatus.Error, HostnameStatus.Verifying,
                t => t.Named("request-recheck"))
            .Transition(HostnameStatus.Active, HostnameStatus.Verifying,
                t => t.Named("request-recheck")));

    private ManagedHostname()
    {
        // Required by EF Core materialisation.
    }

    // ── Identity & ownership ────────────────────────────────────────────────

    /// <summary>The fully-qualified hostname (globally unique, lower-case).</summary>
    public Hostname Host { get; private set; } = null!;

    /// <summary>Opaque owner-resource discriminator (e.g. <c>"cms.site"</c>).</summary>
    public string OwnerType { get; private set; } = string.Empty;

    /// <summary>Identifier of the owning resource within <see cref="OwnerType"/>.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Owning tenant; <c>null</c> for a host-level (global) hostname.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Whether this is the canonical hostname among the owner's hostnames.</summary>
    public bool IsPrimary { get; private set; }

    // ── Workflow state ──────────────────────────────────────────────────────

    /// <summary>Lifecycle state. See <see cref="HostnameStatus"/>.</summary>
    public HostnameStatus Status { get; private set; } = HostnameStatus.Pending;

    /// <summary>Optimistic-concurrency token (ADR-061). Auto-managed by the framework interceptor.</summary>
    public string ConcurrencyStamp { get; private set; } = string.Empty;

    // ── Verification ────────────────────────────────────────────────────────

    /// <summary>
    /// TXT challenge token placed at <c>_granit-challenge.{host}</c>.
    /// Set by <see cref="BeginVerification"/>; <c>null</c> until then.
    /// </summary>
    public string? VerificationToken { get; private set; }

    /// <summary>
    /// DNS records the owner must configure (CNAME / A / TXT entries pointing to the platform).
    /// Set by <see cref="BeginVerification"/>. Stored as JSON.
    /// </summary>
    public IReadOnlyList<ExpectedDnsRecord> ExpectedDnsRecords { get; private set; } = [];

    /// <summary>When the last DNS check ran; <c>null</c> before any check.</summary>
    public DateTimeOffset? LastCheckedAt { get; private set; }

    /// <summary>DNS conflicts detected on the last check. Stored as JSON; empty when verified.</summary>
    public IReadOnlyList<DnsConflict> Conflicts { get; private set; } = [];

    // ── Certificate ─────────────────────────────────────────────────────────

    /// <summary>SSL/TLS certificate provisioning state. Updated by the edge provider webhook.</summary>
    public CertificateStatus CertificateStatus { get; private set; } = CertificateStatus.Unprovisioned;

    /// <summary>
    /// When the active certificate expires; <c>null</c> until a certificate is secured.
    /// Reset to <c>null</c> when the certificate enters <see cref="CertificateStatus.Error"/>.
    /// </summary>
    public DateTimeOffset? CertExpiresAt { get; private set; }

    // ── Backoff ─────────────────────────────────────────────────────────────

    /// <summary>Cumulative count of consecutive DNS check failures.</summary>
    public int FailedCheckCount { get; private set; }

    /// <summary>
    /// Earliest time the poller should retry. <c>null</c> when the domain is dormant
    /// (exceeded <see cref="DormancyThreshold"/>); requires manual
    /// <see cref="RequestRecheck"/> to re-enter <see cref="HostnameStatus.Verifying"/>.
    /// </summary>
    public DateTimeOffset? NextCheckAt { get; private set; }

    // ── Interface implementations ───────────────────────────────────────────

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    string IConcurrencyAware.ConcurrencyStamp
    {
        get => ConcurrencyStamp;
        set => ConcurrencyStamp = value;
    }

    static string IWorkflowStateful.StatusPropertyName => nameof(Status);
    static string IWorkflowStateful.WorkflowEntityType => "ManagedHostname";
    string IWorkflowStateful.GetWorkflowEntityId() => Id.ToString();

    void IWorkflowStateful.RaiseWorkflowStateChangedEvent(string entityType, string previousState, string newState, string transitionedBy) =>
        AddDomainEvent(new WorkflowStateChangedEvent(entityType, Id.ToString(), previousState, newState, transitionedBy));

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a hostname for an owning resource. Starts in <see cref="HostnameStatus.Pending"/>
    /// until <see cref="BeginVerification"/> is called.
    /// </summary>
    /// <param name="id">Stable identifier (from <c>IGuidGenerator</c>).</param>
    /// <param name="host">The hostname (validated, lower-cased).</param>
    /// <param name="ownerType">Owner-resource discriminator (non-empty).</param>
    /// <param name="ownerId">Owning resource id.</param>
    /// <param name="tenantId">Owning tenant; <c>null</c> for a global hostname.</param>
    /// <param name="isPrimary">Whether this hostname is the owner's canonical one.</param>
    public static ManagedHostname Create(
        Guid id,
        Hostname host,
        string ownerType,
        Guid ownerId,
        Guid? tenantId = null,
        bool isPrimary = false)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);

        return new ManagedHostname
        {
            Id = id,
            Host = host,
            OwnerType = ownerType.Trim(),
            OwnerId = ownerId,
            TenantId = tenantId,
            IsPrimary = isPrimary,
            Status = HostnameStatus.Pending,
        };
    }

    // ── Behaviour ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts verification: mints the DNS challenge and expected records, transitions to
    /// <see cref="HostnameStatus.Verifying"/>, resets the failure backoff.
    /// </summary>
    /// <param name="verificationToken">Unique TXT challenge value for <c>_granit-challenge.{host}</c>.</param>
    /// <param name="expectedRecords">DNS records the owner must configure.</param>
    public void BeginVerification(string verificationToken, IReadOnlyList<ExpectedDnsRecord> expectedRecords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationToken);
        ArgumentNullException.ThrowIfNull(expectedRecords);

        Status = HostnameStatus.Verifying;
        VerificationToken = verificationToken;
        ExpectedDnsRecords = expectedRecords;
        FailedCheckCount = 0;
        NextCheckAt = null;
    }

    /// <summary>
    /// Marks verification as successful → <see cref="HostnameStatus.Active"/>.
    /// Raises <see cref="HostnameVerifiedEto"/>.
    /// </summary>
    /// <param name="now">Current timestamp (from <c>TimeProvider</c>).</param>
    public void MarkVerified(DateTimeOffset now)
    {
        Status = HostnameStatus.Active;
        LastCheckedAt = now;
        Conflicts = [];
        FailedCheckCount = 0;
        NextCheckAt = null;
        // Challenge token served its purpose — clear to avoid retaining it indefinitely.
        VerificationToken = null;
        ExpectedDnsRecords = [];

        AddDistributedEvent(new HostnameVerifiedEto(
            Id, Host.Value, OwnerType, OwnerId, TenantId));
    }

    /// <summary>
    /// Records a verification failure → <see cref="HostnameStatus.Error"/> with exponential backoff.
    /// After <see cref="DormancyThreshold"/> failures <see cref="NextCheckAt"/> is set to <c>null</c>
    /// (domain becomes dormant; requires manual <see cref="RequestRecheck"/>).
    /// Raises <see cref="HostnameVerificationFailedEto"/>.
    /// </summary>
    /// <param name="conflicts">Detected DNS conflicts.</param>
    /// <param name="now">Current timestamp (from <c>TimeProvider</c>).</param>
    public void MarkFailed(IReadOnlyList<DnsConflict> conflicts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conflicts);

        Status = HostnameStatus.Error;
        LastCheckedAt = now;
        Conflicts = conflicts;
        FailedCheckCount++;

        NextCheckAt = FailedCheckCount >= DormancyThreshold
            ? null
            : now + BackoffDelay(FailedCheckCount);

        AddDistributedEvent(new HostnameVerificationFailedEto(
            Id, Host.Value, OwnerType, OwnerId, TenantId,
            FailedCheckCount, Conflicts, NextCheckAt));
    }

    /// <summary>
    /// Manually re-queues verification (resets backoff, transitions to
    /// <see cref="HostnameStatus.Verifying"/>). Valid from any state — lets admins
    /// restart a dormant or errored domain without waiting for the poller.
    /// </summary>
    public void RequestRecheck()
    {
        Status = HostnameStatus.Verifying;
        FailedCheckCount = 0;
        NextCheckAt = null;
    }

    /// <summary>
    /// Records a certificate status update from the edge provider.
    /// Raises <see cref="HostnameCertificateSecuredEto"/> when <paramref name="status"/> is
    /// <see cref="CertificateStatus.Secured"/>, or <see cref="HostnameCertificateFailedEto"/>
    /// when it is <see cref="CertificateStatus.Error"/>.
    /// </summary>
    /// <param name="status">New certificate provisioning state.</param>
    /// <param name="expiresAt">Certificate expiry timestamp (required when <paramref name="status"/> is <see cref="CertificateStatus.Secured"/>).</param>
    public void ReportCertificateStatus(CertificateStatus status, DateTimeOffset? expiresAt = null)
    {
        bool statusChanged = CertificateStatus != status;

        CertificateStatus = status;
        CertExpiresAt = status == CertificateStatus.Secured ? expiresAt : null;

        // Guard: edge providers commonly retry webhook delivery. Only raise an integration
        // event when the status actually changes to avoid duplicate downstream notifications.
        if (!statusChanged)
        {
            return;
        }

        switch (status)
        {
            case CertificateStatus.Secured:
                AddDistributedEvent(new HostnameCertificateSecuredEto(
                    Id, Host.Value, OwnerType, OwnerId, TenantId, expiresAt));
                break;
            case CertificateStatus.Error:
                AddDistributedEvent(new HostnameCertificateFailedEto(
                    Id, Host.Value, OwnerType, OwnerId, TenantId));
                break;
        }
    }

    /// <summary>Marks this hostname as the owner's canonical one.</summary>
    public void SetPrimary() => IsPrimary = true;

    /// <summary>Clears the canonical flag.</summary>
    public void ClearPrimary() => IsPrimary = false;

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static TimeSpan BackoffDelay(int failureCount)
    {
        int index = Math.Min(failureCount - 1, BackoffTable.Length - 1);
        return BackoffTable[index];
    }
}
