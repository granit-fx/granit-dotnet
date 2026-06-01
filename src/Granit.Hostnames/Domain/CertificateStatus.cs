namespace Granit.Hostnames.Domain;

/// <summary>
/// SSL/TLS certificate provisioning state for a <see cref="ManagedHostname"/>.
/// Reported by the edge provider via the certificate-status webhook.
/// </summary>
public enum CertificateStatus
{
    /// <summary>No certificate has been provisioned yet. Default state.</summary>
    Unprovisioned,

    /// <summary>The edge provider is issuing the certificate (ACME challenge in progress).</summary>
    Provisioning,

    /// <summary>Certificate is active and HTTPS is live on this hostname.</summary>
    Secured,

    /// <summary>Certificate provisioning failed or the certificate expired.</summary>
    Error,
}
