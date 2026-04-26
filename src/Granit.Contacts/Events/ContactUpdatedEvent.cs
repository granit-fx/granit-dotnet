using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact's identity / address / tax-identity / parent / roles are updated.</summary>
public sealed record ContactUpdatedEvent(ContactId ContactId, Guid? TenantId) : IDomainEvent;
