namespace Granit.Identity;

/// <summary>
/// A recorded device-trust decision for a <c>(userId, deviceId)</c> pair — the value stored in
/// <see cref="IDeviceTrustStore"/> and surfaced on <see cref="UserDevice"/>.
/// </summary>
/// <param name="Level">The trust strength.</param>
/// <param name="TrustedAt">When trust was established.</param>
/// <param name="TrustedUntil">When trust expires; <see langword="null"/> means no explicit expiry.</param>
/// <param name="Reason">Audit-readable reason trust was granted (e.g. <c>"user_marked"</c>, <c>"passkey"</c>).</param>
public sealed record DeviceTrustVerdict(
    DeviceTrustLevel Level,
    DateTimeOffset TrustedAt,
    DateTimeOffset? TrustedUntil,
    string? Reason = null)
{
    /// <summary>
    /// True when the verdict grants trust and has not expired as of <paramref name="now"/>. Callers must
    /// pass a <see cref="TimeProvider"/>-derived value — never <c>DateTimeOffset.UtcNow</c> directly.
    /// </summary>
    public bool IsActive(DateTimeOffset now) =>
        Level is not DeviceTrustLevel.None && (TrustedUntil is null || TrustedUntil > now);
}
