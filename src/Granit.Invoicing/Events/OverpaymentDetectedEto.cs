using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>
/// Published when a payment exceeds the invoice total.
/// The Platform Operator should issue a refund or credit to the next invoice.
/// </summary>
public sealed record OverpaymentDetectedEto(
    Guid InvoiceId, Guid TenantId,
    decimal OverpaymentAmount, string Currency) : IIntegrationEvent;
