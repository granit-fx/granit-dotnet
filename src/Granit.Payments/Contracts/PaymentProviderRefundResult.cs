using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Result of a refund attempt from the provider.</summary>
public sealed record PaymentProviderRefundResult(string ProviderRefundId, RefundStatus Status);
