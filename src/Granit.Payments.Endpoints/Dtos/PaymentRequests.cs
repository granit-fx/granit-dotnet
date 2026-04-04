namespace Granit.Payments.Endpoints.Dtos;

/// <summary>Request to initiate a payment charge for an invoice.</summary>
/// <param name="InvoiceId">The invoice to charge.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">ISO 4217 currency code (e.g. <c>EUR</c>).</param>
/// <param name="MethodType">Payment method type (e.g. <c>card</c>, <c>sepa_debit</c>).</param>
/// <param name="IdempotencyKey">Client-generated idempotency key.</param>
/// <param name="ProviderName">Explicit provider override; <c>null</c> to auto-resolve.</param>
public sealed record PaymentChargeRequest(
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    string MethodType,
    string IdempotencyKey,
    string? ProviderName = null);

/// <summary>Request to refund a payment transaction.</summary>
/// <param name="TransactionId">The transaction to refund.</param>
/// <param name="Amount">Refund amount (partial or full).</param>
/// <param name="Reason">Optional reason for the refund.</param>
/// <param name="IdempotencyKey">Client-generated idempotency key.</param>
public sealed record PaymentRefundRequest(
    Guid TransactionId,
    decimal Amount,
    string? Reason,
    string IdempotencyKey);

/// <summary>Request to create a hosted checkout session.</summary>
/// <param name="TransactionId">The transaction to check out.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="MethodType">Payment method type.</param>
/// <param name="SuccessUrl">Redirect URL on success.</param>
/// <param name="CancelUrl">Redirect URL on cancellation.</param>
/// <param name="ProviderName">Explicit provider override; <c>null</c> to auto-resolve.</param>
public sealed record PaymentCheckoutRequest(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    string MethodType,
    string SuccessUrl,
    string CancelUrl,
    string? ProviderName = null);

/// <summary>Request to attach a payment method to the current tenant.</summary>
/// <param name="ProviderName">Payment provider name (e.g. <c>stripe</c>).</param>
/// <param name="Type">Method type (e.g. <c>card</c>, <c>sepa_debit</c>).</param>
/// <param name="Token">Provider-specific token or setup intent ID.</param>
public sealed record PaymentAttachMethodRequest(
    string ProviderName,
    string Type,
    string Token);
