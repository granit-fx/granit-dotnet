using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>Published when an invoice is overdue (Open + past DueAt).</summary>
public sealed record InvoiceOverdueEto(
    Guid InvoiceId, Guid TenantId, DateTimeOffset DueAt) : IIntegrationEvent;
