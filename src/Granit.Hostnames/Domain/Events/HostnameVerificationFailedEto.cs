using Granit.Events;

namespace Granit.Hostnames.Domain.Events;

/// <summary>
/// Published when a <see cref="ManagedHostname"/> transitions to
/// <see cref="HostnameStatus.Error"/> after a failed DNS verification attempt.
/// </summary>
/// <param name="HostnameId">Identifier of the hostname.</param>
/// <param name="Host">The hostname value (FQDN).</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for a global hostname.</param>
/// <param name="FailedCheckCount">Total consecutive failure count (including this one).</param>
/// <param name="Conflicts">DNS conflicts that caused the failure.</param>
/// <param name="NextCheckAt">When the poller will retry; <c>null</c> if dormant (manual recheck required).</param>
public sealed record HostnameVerificationFailedEto(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId,
    int FailedCheckCount,
    IReadOnlyList<DnsConflict> Conflicts,
    DateTimeOffset? NextCheckAt) : IIntegrationEvent;
