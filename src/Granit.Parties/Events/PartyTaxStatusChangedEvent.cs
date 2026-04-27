using Granit.Events;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>
/// Domain event raised when a party's customer-specific <see cref="TaxStatus"/> changes.
/// Distinct from <see cref="PartyUpdatedEvent"/> so the operations team can observe and
/// alert on tax-status mutations independently — they are a financial-impact operation
/// (Granit.Tax computes 0% rates on every line for exempt / reverse-charge customers).
/// </summary>
public sealed record PartyTaxStatusChangedEvent(
    PartyId PartyId,
    Guid? TenantId,
    TaxStatus Previous,
    TaxStatus Current) : IDomainEvent;
