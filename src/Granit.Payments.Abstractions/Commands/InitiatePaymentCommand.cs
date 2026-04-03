namespace Granit.Payments.Commands;

/// <summary>Command to initiate a payment for an invoice.</summary>
public sealed record InitiatePaymentCommand(
    Guid InvoiceId, Guid TenantId, decimal Amount, string Currency,
    string MethodType, string IdempotencyKey);
