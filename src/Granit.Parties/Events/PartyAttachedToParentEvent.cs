using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a party is attached to a parent party.</summary>
public sealed record PartyAttachedToParentEvent(
    PartyId PartyId,
    Guid? TenantId,
    PartyId ParentPartyId) : IDomainEvent;
