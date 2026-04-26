using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact transitions from <c>Suspended</c> to <c>Active</c>.</summary>
public sealed record ContactActivatedEvent(ContactId ContactId, Guid? TenantId) : IDomainEvent;
