using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Integration event for contact suspension.</summary>
public sealed record PartySuspendedEto(
    PartyId PartyId,
    Guid? TenantId,
    string? Reason) : IIntegrationEvent;
