using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact is detached from its parent.</summary>
public sealed record ContactDetachedFromParentEvent(
    ContactId ContactId,
    Guid? TenantId,
    ContactId FormerParentContactId) : IDomainEvent;
