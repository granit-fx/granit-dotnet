using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact is attached to a parent contact.</summary>
public sealed record ContactAttachedToParentEvent(
    ContactId ContactId,
    Guid? TenantId,
    ContactId ParentContactId) : IDomainEvent;
