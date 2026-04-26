using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact is archived (terminal state).</summary>
public sealed record ContactArchivedEvent(ContactId ContactId, Guid? TenantId) : IDomainEvent;
