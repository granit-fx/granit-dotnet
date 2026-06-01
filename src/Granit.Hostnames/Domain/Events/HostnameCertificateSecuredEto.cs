using Granit.Events;

namespace Granit.Hostnames.Domain.Events;

/// <summary>
/// Published when an edge provider reports that the SSL/TLS certificate for a
/// <see cref="ManagedHostname"/> has been successfully issued.
/// </summary>
/// <param name="HostnameId">Identifier of the secured hostname.</param>
/// <param name="Host">The hostname value (FQDN).</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for a global hostname.</param>
/// <param name="ExpiresAt">When the issued certificate expires; <c>null</c> when unknown.</param>
public sealed record HostnameCertificateSecuredEto(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId,
    DateTimeOffset? ExpiresAt) : IIntegrationEvent;
