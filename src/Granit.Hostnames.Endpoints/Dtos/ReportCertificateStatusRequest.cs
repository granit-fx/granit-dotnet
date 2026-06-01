using Granit.Hostnames.Domain;

namespace Granit.Hostnames.Endpoints.Dtos;

/// <summary>
/// Payload for the <c>POST /hostnames/{id}/certificate-status</c> webhook endpoint.
/// Sent by the edge provider when the SSL/TLS certificate state changes.
/// </summary>
/// <param name="Status">New certificate provisioning state.</param>
/// <param name="ExpiresAt">Certificate expiry timestamp. Required when <paramref name="Status"/> is <see cref="CertificateStatus.Secured"/>.</param>
public sealed record ReportCertificateStatusRequest(
    CertificateStatus Status,
    DateTimeOffset? ExpiresAt = null);
