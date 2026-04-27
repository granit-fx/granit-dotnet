using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a contact is archived (terminal state).</summary>
public sealed record PartyArchivedEvent(PartyId PartyId, Guid? TenantId) : IDomainEvent;
