using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a contact is detached from its parent.</summary>
public sealed record PartyDetachedFromParentEvent(
    PartyId PartyId,
    Guid? TenantId,
    PartyId FormerParentContactId) : IDomainEvent;
