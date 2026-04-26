using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a role flag is added to a contact.</summary>
public sealed record ContactRoleAddedEvent(
    ContactId ContactId,
    Guid? TenantId,
    ContactRoles Role) : IDomainEvent;
