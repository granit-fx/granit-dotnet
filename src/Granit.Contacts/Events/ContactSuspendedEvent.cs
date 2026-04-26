using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact is suspended.</summary>
/// <param name="ContactId">Contact identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped contacts.</param>
/// <param name="Reason">Optional free-text justification.</param>
public sealed record ContactSuspendedEvent(
    ContactId ContactId,
    Guid? TenantId,
    string? Reason) : IDomainEvent;
