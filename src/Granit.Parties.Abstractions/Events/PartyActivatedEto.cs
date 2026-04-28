using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Integration event for party reactivation.</summary>
public sealed record PartyActivatedEto(PartyId PartyId, Guid? TenantId) : IIntegrationEvent;
