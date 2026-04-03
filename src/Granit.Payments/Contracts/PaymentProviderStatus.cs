using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Current payment status from the provider.</summary>
public sealed record PaymentProviderStatus(string ProviderTransactionId, PaymentStatus Status);
