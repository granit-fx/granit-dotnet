namespace Granit.Identity.Endpoints.Options;

/// <summary>
/// Configuration for device trust — the "remember this device" capability surfaced by the canonical
/// <c>/devices</c> API and consulted by the step-up decision. Bind from <c>"Identity:DeviceTrust"</c>.
/// </summary>
public sealed class DeviceTrustOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:DeviceTrust";

    /// <summary>
    /// How long a "trust this device" decision remains valid. The signed device cookie and the stored verdict
    /// both carry this lifetime. Default: 30 days.
    /// </summary>
    public TimeSpan TrustDuration { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Whether a trusted device may bypass the two-factor step-up challenge on login. <see langword="false"/> by
    /// default — bypassing MFA is a deliberate security-posture relaxation (ISO 27001 A.9.4) a deployment must
    /// opt into. When <see langword="false"/>, device trust still reduces friction elsewhere (e.g. risk scoring)
    /// but never skips 2FA.
    /// </summary>
    public bool AllowMfaBypass { get; set; }

    /// <summary>
    /// Name of the signed device-trust cookie. Default: <c>"__Host-id-devicetrust"</c> (the <c>__Host-</c> prefix
    /// requires HTTPS + <c>Path=/</c> + no <c>Domain</c>). Override to a non-prefixed name for plain-HTTP dev.
    /// </summary>
    public string CookieName { get; set; } = "__Host-id-devicetrust";
}
