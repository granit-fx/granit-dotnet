using Granit.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published when a data subject requests export of their personal data (GDPR Art. 15/20,
/// LGPD Art. 18, CCPA). Each registered data provider handles this event and prepares
/// its fragment when its scope is in <see cref="RequestedScopes"/> (or when
/// <see cref="RequestedScopes"/> is <see langword="null"/> — Takeout-style "all visible").
/// </summary>
/// <param name="RequestId">Saga correlation id.</param>
/// <param name="UserId">Data subject whose data is being exported.</param>
/// <param name="RequestedAt">When the subject filed the request.</param>
/// <param name="Regulation">Regulation code under which the request is processed.</param>
/// <param name="TenantId">Tenant scope, as currently typed in this Eto
/// (<see cref="string"/>?). Will widen to <see cref="Guid"/>? in a follow-up alongside
/// the metrics-signature migration.</param>
/// <param name="RequestedFormat">Wire format requested by the subject (default JSON).</param>
/// <param name="RequestedScopes">Subset of provider names the subject asked for.
/// <see langword="null"/> means "all scopes visible to me" (Takeout-style default).
/// The saga intersects this list with the visibility resolver's output — unknown or
/// hidden scopes are silently skipped and recorded in the audit trail (VULN-202: no
/// echo back to the caller, no enumeration leak).</param>
public sealed record PersonalDataRequestedEto(
    [property: SagaIdentity] Guid RequestId,
    Guid UserId,
    DateTimeOffset RequestedAt,
    string Regulation,
    string? TenantId = null,
    string RequestedFormat = "JSON",
    IReadOnlyList<string>? RequestedScopes = null) : IIntegrationEvent;
