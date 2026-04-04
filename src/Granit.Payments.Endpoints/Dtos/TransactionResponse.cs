using Granit.Payments.Domain;

namespace Granit.Payments.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="PaymentTransaction"/> for API responses.
/// </summary>
public sealed record PaymentTransactionResponse(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string ProviderName,
    string? ProviderTransactionId,
    Guid? PaymentMethodId,
    string? ActionUrl,
    string IdempotencyKey,
    string? FailureCode,
    DateTimeOffset? SucceededAt,
    DateTimeOffset? CanceledAt,
    IReadOnlyList<PaymentRefundResponse> Refunds,
    IReadOnlyList<PaymentDisputeResponse> Disputes,
    Guid? TenantId);

/// <summary>Read-only representation of a <see cref="Refund"/>.</summary>
public sealed record PaymentRefundResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    RefundStatus Status,
    string? ProviderRefundId,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Response containing a hosted checkout session URL and metadata.</summary>
public sealed record PaymentCheckoutSessionResponse(
    string Url,
    string SessionId,
    DateTimeOffset ExpiresAt);

/// <summary>Read-only representation of a <see cref="Dispute"/>.</summary>
public sealed record PaymentDisputeResponse(
    Guid Id,
    string ProviderDisputeId,
    DisputeStatus Status,
    string Reason,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
