using Granit.Payments.Contracts;

namespace Granit.Payments;

/// <summary>Creates hosted payment page sessions (redirect to Stripe/Mollie/bank instructions).</summary>
public interface ICheckoutSessionFactory
{
    /// <summary>Provider name this factory serves.</summary>
    string ProviderName { get; }

    /// <summary>Creates a checkout session.</summary>
    Task<PaymentCheckoutSession> CreateAsync(PaymentCheckoutSessionRequest request, CancellationToken cancellationToken = default);
}
