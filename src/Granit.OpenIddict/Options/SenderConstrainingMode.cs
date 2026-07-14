namespace Granit.OpenIddict.Options;

/// <summary>
/// The sender-constraining (proof-of-possession) mechanism the OpenIddict server binds
/// issued access tokens to. FAPI 2.0 §5.3.2 requires one; each mechanism ships as its own
/// opt-in package that the host references and that registers itself with the server.
/// </summary>
public enum SenderConstrainingMode
{
    /// <summary>No sender-constraining. Bearer tokens only. Not permitted under FAPI 2.0.</summary>
    None,

    /// <summary>
    /// DPoP (RFC 9449). Provided by the <c>Granit.OpenIddict.Server.DPoP</c> package.
    /// </summary>
    DPoP,

    /// <summary>
    /// Mutual-TLS certificate-bound tokens (RFC 8705). Reserved for a future
    /// <c>Granit.OpenIddict.Server.Mtls</c> package.
    /// </summary>
    Mtls,
}
