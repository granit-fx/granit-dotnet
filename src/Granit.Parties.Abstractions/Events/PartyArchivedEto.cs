using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Integration event for party archival.</summary>
public sealed record PartyArchivedEto(PartyId PartyId, Guid? TenantId) : IIntegrationEvent;
