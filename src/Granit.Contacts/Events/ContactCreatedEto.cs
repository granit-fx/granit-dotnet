using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>
/// Integration event published when a new contact is created. Downstream modules consume
/// this to react to contact creation (e.g., provision a default payment method placeholder,
/// seed metering counters, register a new "customer" in a CRM via Wolverine outbox).
/// </summary>
public sealed record ContactCreatedEto(
    ContactId ContactId,
    Guid? TenantId,
    ContactKind Kind,
    string Name,
    ContactRoles Roles,
    string DefaultCurrency) : IIntegrationEvent;
