using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a contact is suspended.</summary>
/// <param name="PartyId">Party identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped contacts.</param>
/// <param name="Reason">Optional free-text justification.</param>
public sealed record PartySuspendedEvent(
    PartyId PartyId,
    Guid? TenantId,
    string? Reason) : IDomainEvent;
