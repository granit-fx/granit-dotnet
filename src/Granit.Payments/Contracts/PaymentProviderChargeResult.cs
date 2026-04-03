using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Result of a charge attempt from the provider.</summary>
public sealed record PaymentProviderChargeResult(
    string ProviderTransactionId, ProviderChargeStatus Status, string? ActionUrl = null);
