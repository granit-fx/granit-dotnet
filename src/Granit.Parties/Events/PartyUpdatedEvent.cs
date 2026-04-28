using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a party's identity / address / tax-identity / parent / roles are updated.</summary>
public sealed record PartyUpdatedEvent(PartyId PartyId, Guid? TenantId) : IDomainEvent;
