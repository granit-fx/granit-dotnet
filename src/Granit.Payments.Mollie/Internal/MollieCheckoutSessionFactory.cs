using Granit.Payments.Contracts;

namespace Granit.Payments.Mollie.Internal;

internal sealed class MollieCheckoutSessionFactory : ICheckoutSessionFactory
{
    public string ProviderName => "mollie";

    public Task<PaymentCheckoutSession> CreateAsync(PaymentCheckoutSessionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Mollie checkout not configured.");
}
