using Granit.Events;

namespace Granit.Hostnames.Domain.Events;

/// <summary>
/// Published when an edge provider reports that SSL/TLS certificate provisioning for a
/// <see cref="ManagedHostname"/> has failed (or that a previously active certificate has expired).
/// </summary>
/// <param name="HostnameId">Identifier of the affected hostname.</param>
/// <param name="Host">The hostname value (FQDN).</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for a global hostname.</param>
public sealed record HostnameCertificateFailedEto(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId) : IIntegrationEvent;
