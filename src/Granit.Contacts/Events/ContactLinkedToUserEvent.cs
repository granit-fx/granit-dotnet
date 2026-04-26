using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when an Individual contact is linked to an authenticated user.</summary>
public sealed record ContactLinkedToUserEvent(
    ContactId ContactId,
    Guid? TenantId,
    Guid UserId) : IDomainEvent;
