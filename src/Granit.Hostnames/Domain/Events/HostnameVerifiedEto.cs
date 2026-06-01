using Granit.Events;

namespace Granit.Hostnames.Domain.Events;

/// <summary>
/// Published when a <see cref="ManagedHostname"/> transitions to
/// <see cref="HostnameStatus.Active"/> after successful DNS verification.
/// </summary>
/// <param name="HostnameId">Identifier of the verified hostname.</param>
/// <param name="Host">The verified hostname value (FQDN).</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for a global hostname.</param>
public sealed record HostnameVerifiedEto(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId) : IIntegrationEvent;
