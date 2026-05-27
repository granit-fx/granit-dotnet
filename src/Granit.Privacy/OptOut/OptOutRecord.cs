using Granit.DataProtection;

namespace Granit.Privacy.OptOut;

/// <summary>
/// Read model for an opt-out record. Supports both authenticated users (<see cref="UserId"/>)
/// and anonymous visitors (<see cref="AnonymousTrackId"/>) for CCPA guest opt-out compliance.
/// </summary>
/// <param name="Id">Unique record identifier.</param>
/// <param name="UserId">Authenticated user ID, or <c>null</c> for anonymous visitors.</param>
/// <param name="AnonymousTrackId">Anonymous tracking ID stored in <c>_optout_id</c> cookie, or <c>null</c> for authenticated users.
/// Treated as personal data under GDPR Recital 30 (online identifier) — hashed when surfaced through
/// logs / audit so investigators can still correlate without leaking the cookie value.</param>
/// <param name="State">Current opt-out state.</param>
/// <param name="RequestedAt">When the opt-out was requested (UTC).</param>
/// <param name="RevokedAt">When the opt-out was revoked (UTC), or <c>null</c> if still active.</param>
/// <param name="TenantId">Tenant identifier for multi-tenant isolation.</param>
/// <param name="Regulation">Applicable regulation code (e.g., <c>"US_CCPA"</c>).</param>
public sealed record OptOutRecord(
    Guid Id,
    Guid? UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash)]
    string? AnonymousTrackId,
    OptOutState State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? RevokedAt,
    Guid? TenantId,
    string Regulation);
