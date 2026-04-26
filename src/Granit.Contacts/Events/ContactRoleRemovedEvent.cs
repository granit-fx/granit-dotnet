using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a role flag is removed from a contact.</summary>
public sealed record ContactRoleRemovedEvent(
    ContactId ContactId,
    Guid? TenantId,
    ContactRoles Role) : IDomainEvent;
