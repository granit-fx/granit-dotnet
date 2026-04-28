using Granit.DataProtection;
using Granit.Events;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>
/// Integration event published when a new party is created. Downstream modules consume
/// this to react to party creation (e.g., provision a default payment method placeholder,
/// seed metering counters, register a new "customer" in a CRM via Wolverine outbox).
/// </summary>
public sealed record PartyCreatedEto(
    PartyId PartyId,
    Guid? TenantId,
    PartyKind Kind,
    [property: SensitiveData(Level = Sensitivity.Internal)] string Name,
    PartyRoles Roles,
    string DefaultCurrency) : IIntegrationEvent;
