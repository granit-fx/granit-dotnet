using Granit.Events;

namespace Granit.MultiTenancy.Events;

/// <summary>
/// Integration event published durably via the Wolverine outbox when a new tenant is created.
/// </summary>
/// <remarks>
/// Distinct from <see cref="TenantCreatedEvent"/>: that domain event is an in-process,
/// non-durable notification hook dispatched after commit; this ETO is the durable trigger
/// consumed by <c>Granit.MultiTenancy.Provisioning</c> to run tenant schema migrations and
/// seeding. Because it rides the outbox (written atomically with the <c>Tenant</c> row), a
/// crash between the host-DB commit and provisioning completion is recovered on restart —
/// closing the gap a non-durable in-process event would leave open.
/// </remarks>
/// <param name="TenantId">The unique identifier of the tenant.</param>
/// <param name="Name">Display name of the tenant.</param>
/// <param name="Identifier">Unique slug/subdomain identifier.</param>
public sealed record TenantCreatedEto(
    Guid TenantId,
    string Name,
    string Identifier) : IIntegrationEvent;
