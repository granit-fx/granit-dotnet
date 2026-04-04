namespace Granit.Payments.Commands;

/// <summary>Command to initiate a payment for an invoice.</summary>
/// <param name="InvoiceId">The invoice to pay.</param>
/// <param name="TenantId">The tenant.</param>
/// <param name="Amount">The amount to charge.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="MethodType">Payment method type (e.g., "card", "sepa_debit").</param>
/// <param name="IdempotencyKey">Idempotency key for the charge.</param>
/// <param name="ProviderName">Explicit provider override. Null = resolve via IPaymentProviderResolver.</param>
public sealed record InitiatePaymentCommand(
    Guid InvoiceId, Guid TenantId, decimal Amount, string Currency,
    string MethodType, string IdempotencyKey,
    string? ProviderName = null);
