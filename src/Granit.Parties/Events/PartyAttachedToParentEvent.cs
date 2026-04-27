using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a contact is attached to a parent contact.</summary>
public sealed record PartyAttachedToParentEvent(
    PartyId PartyId,
    Guid? TenantId,
    PartyId ParentContactId) : IDomainEvent;
