using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a party transitions from <c>Suspended</c> to <c>Active</c>.</summary>
public sealed record PartyActivatedEvent(PartyId PartyId, Guid? TenantId) : IDomainEvent;
