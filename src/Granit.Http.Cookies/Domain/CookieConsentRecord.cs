using Granit.Auditing.Attributes;
using Granit.Domain;

namespace Granit.Http.Cookies.Domain;

/// <summary>
/// Append-only, server-side record of a single cookie-consent decision — the GDPR
/// Art. 7(1) accountability evidence that consent was given (or refused), independent
/// of the CMP's client-side cookie.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the <c>AuditEntry</c> shape: inherits <see cref="CreationAuditedEntity"/>
/// for Id, CreatedAt, and CreatedBy (auto-populated by <c>AuditedEntityInterceptor</c>);
/// records are immutable once written — no modification audit fields.
/// <c>[AuditIgnore]</c> keeps the change-tracking audit interceptor from double-writing
/// the ledger into the general audit trail: the ledger IS its own trail.
/// </para>
/// <para>
/// Data minimisation by design: <see cref="AnonymizedIp"/> MUST be pre-anonymized by the
/// caller (irreversible <c>IpAddressAnonymizer</c> masking at the endpoint layer — this
/// entity only stores the string), and <see cref="UserAgent"/> is truncated to
/// <see cref="MaxUserAgentLength"/> by the factory. The record carries no user identifier
/// field; the only potential subject link is the infrastructure <c>CreatedBy</c> audit
/// field, stamped when an authenticated user posts a decision.
/// </para>
/// </remarks>
[AuditIgnore]
public class CookieConsentRecord : CreationAuditedEntity, IMultiTenant
{
    /// <summary>Maximum stored length of the client User-Agent header.</summary>
    public const int MaxUserAgentLength = 256;

    /// <summary>Cookie categories the user granted, as snake_case category names.</summary>
    public IReadOnlyList<string> GrantedCategories { get; private set; } = [];

    /// <summary>Cookie categories the user denied, as snake_case category names.</summary>
    public IReadOnlyList<string> DeniedCategories { get; private set; } = [];

    /// <summary>Consent model under which the decision was taken (OptIn, OptOut, ...).</summary>
    public CookieConsentMode Mode { get; private set; }

    /// <summary>Identifier of the CMP that captured the decision (e.g. <c>"cookieconsent"</c>).</summary>
    public string CmpSource { get; private set; } = string.Empty;

    /// <summary>
    /// Pre-anonymized client IP (IPv4 /24, IPv6 /48 — irreversible truncation performed
    /// by the caller, never stored raw).
    /// </summary>
    public string? AnonymizedIp { get; private set; }

    /// <summary>Client User-Agent header, truncated to <see cref="MaxUserAgentLength"/> characters.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Distributed tracing correlation identifier.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>When the user took the consent decision (UTC).</summary>
    public DateTimeOffset DecidedAt { get; private set; }

    /// <summary>Owning tenant; stamped by the interceptor.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Required by EF Core materialization — never call from application code.</summary>
    private CookieConsentRecord() { }

    /// <summary>
    /// Creates an immutable consent record. Category lists are defensively copied
    /// (duplicates removed) and the user-agent is truncated to
    /// <see cref="MaxUserAgentLength"/> characters.
    /// </summary>
    /// <param name="grantedCategories">Snake_case names of the granted categories.</param>
    /// <param name="deniedCategories">Snake_case names of the denied categories.</param>
    /// <param name="mode">Consent model under which the decision was taken.</param>
    /// <param name="cmpSource">Identifier of the CMP that captured the decision.</param>
    /// <param name="decidedAt">When the user took the decision (UTC).</param>
    /// <param name="anonymizedIp">Pre-anonymized client IP — never pass a raw address.</param>
    /// <param name="userAgent">Client User-Agent header; truncated on capture.</param>
    /// <param name="correlationId">Distributed tracing correlation identifier.</param>
    public static CookieConsentRecord Create(
        IEnumerable<string> grantedCategories,
        IEnumerable<string> deniedCategories,
        CookieConsentMode mode,
        string cmpSource,
        DateTimeOffset decidedAt,
        string? anonymizedIp = null,
        string? userAgent = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(grantedCategories);
        ArgumentNullException.ThrowIfNull(deniedCategories);
        ArgumentException.ThrowIfNullOrEmpty(cmpSource);

        return new CookieConsentRecord
        {
            GrantedCategories = [.. grantedCategories.Distinct(StringComparer.Ordinal)],
            DeniedCategories = [.. deniedCategories.Distinct(StringComparer.Ordinal)],
            Mode = mode,
            CmpSource = cmpSource,
            DecidedAt = decidedAt,
            AnonymizedIp = anonymizedIp,
            UserAgent = userAgent is { Length: > MaxUserAgentLength }
                ? userAgent[..MaxUserAgentLength]
                : userAgent,
            CorrelationId = correlationId,
        };
    }
}
