using Granit.Payments.Contracts;

namespace Granit.Payments;

/// <summary>Payment provider abstraction (Stripe, Mollie, SEPA, etc.).</summary>
public interface IPaymentProvider
{
    /// <summary>Provider name (e.g., "stripe", "mollie", "sepa-transfer").</summary>
    string Name { get; }

    /// <summary>Initiates a charge.</summary>
    Task<PaymentProviderChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Initiates a refund.</summary>
    Task<PaymentProviderRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the current payment status from the provider.</summary>
    Task<PaymentProviderStatus> GetStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default);
}
