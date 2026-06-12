using Granit.Events;

namespace Granit.UserSessions;

/// <summary>
/// Integration event raised when a session is assessed as <c>Medium</c> or <c>High</c> risk. Consumers
/// (notifications, step-up authentication) subscribe to react.
/// </summary>
/// <remarks>
/// <para>
/// Lives in <c>Granit.UserSessions.Abstractions</c> so a subscriber can react to suspicious sessions without
/// referencing the anomaly-detection engine (and transitively <c>Granit.AI</c>).
/// </para>
/// <para>
/// Carries the coarse context a user-facing alert needs to be meaningful (why it was flagged, where, and on what
/// kind of device) as a self-contained snapshot: <see cref="City"/> / <see cref="CountryCode"/> are the
/// privacy-friendlier derived location — the raw IP is <b>not</b> carried unless the deployment opts into raw-IP
/// exposure (<see cref="IpAddress"/>), and is never logged.
/// </para>
/// </remarks>
/// <param name="UserId">Subject the session belongs to.</param>
/// <param name="SessionId">The flagged session.</param>
/// <param name="TenantId">Tenant the session belongs to; consumers must establish this scope before acting.</param>
/// <param name="Level">Risk level (<c>Medium</c> or <c>High</c>) — drives the consumer's tier and tone.</param>
/// <param name="Reasons">Machine-readable reason codes (e.g. <c>"impossible_travel"</c>, <c>"new_country"</c>), most significant first.</param>
/// <param name="RiskScore">Normalized risk score in <c>[0, 1]</c>.</param>
/// <param name="City">Approximate city of the sign-in, when resolved.</param>
/// <param name="CountryCode">Approximate country (ISO code) of the sign-in, when resolved.</param>
/// <param name="UserAgent">User-Agent captured at establishment, when available (raw signal for downstream device labelling).</param>
/// <param name="IpAddress">Raw client IP, carried only when raw-IP exposure is enabled; otherwise <see langword="null"/>. Never logged.</param>
/// <param name="DetectedAt">When the verdict was produced.</param>
public sealed record SuspiciousUserSessionDetectedEto(
    string UserId,
    string SessionId,
    Guid? TenantId,
    UserSessionRiskLevel Level,
    IReadOnlyList<string> Reasons,
    double RiskScore,
    string? City,
    string? CountryCode,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset DetectedAt) : IIntegrationEvent;
