namespace Granit.Identity;

/// <summary>
/// How strongly a device's identity is trusted for the current user. Higher levels carry a
/// stronger device-binding guarantee and may unlock more friction reduction (e.g. step-up bypass).
/// </summary>
public enum DeviceTrustLevel
{
    /// <summary>No trust recorded — the device is treated as new/unknown.</summary>
    None,

    /// <summary>
    /// Trust established from a signed, revocable device token ("remember this device"). Bound to a
    /// data-protected cookie, time-bounded; spoofable only by exfiltrating the token.
    /// </summary>
    Remembered,

    /// <summary>
    /// Trust backed by a device-bound credential (passkey / WebAuthn) — the strongest signal, since the
    /// credential cannot leave the authenticator.
    /// </summary>
    Strong,
}
