namespace Granit.Authentication.Mtls.Options;

/// <summary>
/// Options for mutual-TLS certificate-bound token validation (RFC 8705).
/// </summary>
public sealed class MtlsValidationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication:Mtls";

    /// <summary>
    /// When <see langword="true"/>, every authenticated request must carry a certificate-bound
    /// token (a <c>cnf.x5t#S256</c> confirmation claim). Plain Bearer tokens are rejected. Default:
    /// <see langword="false"/> — unbound tokens pass through, and only certificate-bound tokens are
    /// verified against the presented client certificate. Set to <see langword="true"/> to enforce
    /// sender-constraining under FAPI 2.0.
    /// </summary>
    public bool RequireCertificateBinding { get; set; }
}
