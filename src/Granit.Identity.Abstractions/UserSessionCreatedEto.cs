using Granit.DataProtection;
using Granit.Encryption;
using Granit.Events;

namespace Granit.Identity;

/// <summary>
/// Integration event raised when a user session is established, from any session source (BFF, OpenIddict,
/// Keycloak). The canonical decoupling point: consumers react out-of-band to a new session without the login
/// path waiting on them.
/// </summary>
/// <remarks>
/// <para>
/// Lives in <c>Granit.Identity.Abstractions</c> so a consumer (anomaly detection, geo enrichment,
/// notifications) can subscribe without referencing any session-provider package. Distributed via
/// <c>AddDistributedEvent</c> / <c>IDistributedEventBus</c> (Wolverine outbox).
/// </para>
/// <para>
/// <see cref="IpAddress"/> is carried so an asynchronous consumer can resolve geolocation off the login critical
/// path — it is server-side only, an internal event payload, and must never be logged in clear or exposed to a
/// browser. The privacy-friendlier derived location is produced downstream (e.g. on
/// <see cref="SuspiciousUserSessionDetectedEto"/>), not here.
/// </para>
/// </remarks>
/// <param name="UserId">Subject the session belongs to.</param>
/// <param name="SessionId">The newly established session.</param>
/// <param name="TenantId">Tenant the session belongs to, when multi-tenant; consumers must establish this scope before acting.</param>
/// <param name="Source">The session layer that established the session.</param>
/// <param name="UserAgent">User-Agent captured at establishment, when available (raw signal for downstream device labelling).</param>
/// <param name="IpAddress">Raw client IP (server-side only) for off-critical-path geo resolution; never logged or exposed.</param>
/// <param name="CreatedAt">When the session was established.</param>
/// <param name="DeviceId">
/// Stable device identifier resolved at the HTTP boundary (from the signed device-trust cookie), when present.
/// Lets an asynchronous consumer (anomaly detection) look up device trust off the login critical path. Absent
/// for sessions established without a browser device cookie (e.g. a pure IdP webhook).
/// </param>
public sealed record UserSessionCreatedEto(
    string UserId,
    string SessionId,
    Guid? TenantId,
    UserSessionSource Source,
    [property: SensitiveData]
    string? UserAgent,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask), Encrypted]
    string? IpAddress,
    DateTimeOffset CreatedAt,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash), Encrypted]
    string? DeviceId = null) : IIntegrationEvent;
