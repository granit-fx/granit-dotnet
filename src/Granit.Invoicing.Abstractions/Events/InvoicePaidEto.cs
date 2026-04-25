using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>Published when an invoice is fully paid.</summary>
public sealed record InvoicePaidEto(
    Guid InvoiceId, Guid TenantId, DateTimeOffset PaidAt) : IIntegrationEvent;
