using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>
/// Integration event for customer suspension — downstream consumers should freeze
/// active subscriptions, halt dunning, and reject new charges. <c>Reason</c> is intended
/// for audit/audit-trail consumption only and should not be surfaced to end-users.
/// </summary>
public sealed record CustomerSuspendedEto(
    CustomerId CustomerId,
    Guid? TenantId,
    string? Reason) : IIntegrationEvent;
