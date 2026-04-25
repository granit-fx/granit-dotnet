using Granit.Events;

namespace Granit.Invoicing.Abstractions.Events;

/// <summary>Published when an invoice is fully paid.</summary>
public sealed record InvoicePaidEto(
    Guid InvoiceId, Guid TenantId, DateTimeOffset PaidAt) : IIntegrationEvent;
