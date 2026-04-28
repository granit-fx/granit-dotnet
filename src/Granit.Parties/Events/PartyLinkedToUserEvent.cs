using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when an Individual party is linked to an authenticated user.</summary>
public sealed record PartyLinkedToUserEvent(
    PartyId PartyId,
    Guid? TenantId,
    Guid UserId) : IDomainEvent;
