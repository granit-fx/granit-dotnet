using Granit.Events;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a new <c>Party</c> aggregate is created.</summary>
public sealed record PartyCreatedEvent(
    PartyId PartyId,
    Guid? TenantId,
    PartyKind Kind,
    string Name,
    PartyRoles Roles) : IDomainEvent;
