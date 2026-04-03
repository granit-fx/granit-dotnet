using Granit.Payments.Dtos;

namespace Granit.Payments;

/// <summary>Creates hosted payment page sessions (redirect to Stripe/Mollie/bank instructions).</summary>
public interface ICheckoutSessionFactory
{
    /// <summary>Provider name this factory serves.</summary>
    string ProviderName { get; }

    /// <summary>Creates a checkout session.</summary>
    Task<CheckoutSession> CreateAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default);
}
