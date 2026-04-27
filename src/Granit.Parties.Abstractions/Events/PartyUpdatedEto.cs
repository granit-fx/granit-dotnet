using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Integration event published when a contact's billing identity is updated.</summary>
public sealed record PartyUpdatedEto(PartyId PartyId, Guid? TenantId) : IIntegrationEvent;
