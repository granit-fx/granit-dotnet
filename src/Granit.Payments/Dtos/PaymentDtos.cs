using System.Text.Json;
using Granit.Payments.Domain;

namespace Granit.Payments.Dtos;

/// <summary>Request to charge a customer.</summary>
public sealed record ChargeRequest(
    Guid TransactionId, decimal Amount, string Currency,
    string IdempotencyKey, Guid? PaymentMethodId = null, string? ReturnUrl = null);

/// <summary>Result of a charge attempt from the provider.</summary>
public sealed record ProviderChargeResult(
    string ProviderTransactionId, ProviderChargeStatus Status, string? ActionUrl = null);

/// <summary>Provider charge status.</summary>
public enum ProviderChargeStatus { Succeeded, RequiresAction, Processing, Failed }

/// <summary>Request to refund a payment.</summary>
public sealed record RefundRequest(
    string ProviderTransactionId, decimal Amount, string IdempotencyKey);

/// <summary>Result of a refund attempt from the provider.</summary>
public sealed record ProviderRefundResult(string ProviderRefundId, RefundStatus Status);

/// <summary>Current payment status from the provider.</summary>
public sealed record ProviderPaymentStatus(string ProviderTransactionId, PaymentStatus Status);

/// <summary>Request to create a checkout session.</summary>
public sealed record CheckoutSessionRequest(
    Guid TransactionId, decimal Amount, string Currency,
    PaymentMethodType MethodType, string SuccessUrl, string CancelUrl);

/// <summary>Checkout session result (redirect URL).</summary>
public sealed record CheckoutSession(string Url, string SessionId, DateTimeOffset ExpiresAt);

/// <summary>Request to attach a payment method.</summary>
public sealed record AttachPaymentMethodRequest(
    Guid TenantId, PaymentMethodType Type, string Token);

/// <summary>Payment method from provider.</summary>
public sealed record ProviderPaymentMethod(
    string ProviderMethodId, PaymentMethodType Type,
    string DisplayLabel, DateTimeOffset? ExpiresAt);

/// <summary>Available payment method for checkout UI.</summary>
public sealed record AvailablePaymentMethod(
    PaymentMethodType Type, string ProviderName, string DisplayLabel);

/// <summary>Webhook verification result.</summary>
public sealed record WebhookVerificationResult(
    bool IsValid, string? EventType, string? ProviderEventId,
    JsonElement Payload, string? RejectionReason);
