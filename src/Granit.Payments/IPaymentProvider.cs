using Granit.Payments.Contracts;

namespace Granit.Payments;

/// <summary>Payment provider abstraction (Stripe, Mollie, SEPA, etc.).</summary>
public interface IPaymentProvider
{
    /// <summary>Provider name (e.g., "stripe", "mollie", "sepa-transfer").</summary>
    string Name { get; }

    /// <summary>
    /// Payment methods this provider supports. Used by the admin to select which
    /// methods to activate on the platform. Labels are resolved via localization
    /// using <c>Payments.Methods.{methodType}</c> keys.
    /// </summary>
    IReadOnlyList<PaymentMethodDescriptor> SupportedMethods { get; }

    /// <summary>
    /// Fetches the live catalog of methods from the provider, with capability metadata
    /// (supported countries, currencies, amount bounds, sequence types).
    /// </summary>
    /// <remarks>
    /// Called by the admin activation flow to snapshot capability into
    /// <c>PaymentMethodConfiguration</c>. External-API-backed providers (Mollie, Stripe)
    /// should honor the provider's current account state (enabled methods in the provider
    /// dashboard). Built-in providers (SEPA) return a static catalog.
    /// </remarks>
    Task<IReadOnlyList<PaymentMethodCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Initiates a charge.</summary>
    Task<PaymentProviderChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Initiates a refund.</summary>
    Task<PaymentProviderRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the current payment status from the provider.</summary>
    Task<PaymentProviderStatus> GetStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default);
}
