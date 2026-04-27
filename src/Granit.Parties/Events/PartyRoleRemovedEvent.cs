using Granit.Events;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a role flag is removed from a contact.</summary>
public sealed record PartyRoleRemovedEvent(
    PartyId PartyId,
    Guid? TenantId,
    PartyRoles Role) : IDomainEvent;
