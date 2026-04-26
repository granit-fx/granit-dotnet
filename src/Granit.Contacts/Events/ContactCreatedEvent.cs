using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a new <c>Contact</c> aggregate is created.</summary>
public sealed record ContactCreatedEvent(
    ContactId ContactId,
    Guid? TenantId,
    ContactKind Kind,
    string Name,
    ContactRoles Roles) : IDomainEvent;
