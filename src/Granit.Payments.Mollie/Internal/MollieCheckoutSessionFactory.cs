using Granit.Payments.Dtos;

namespace Granit.Payments.Mollie.Internal;

internal sealed class MollieCheckoutSessionFactory : ICheckoutSessionFactory
{
    public string ProviderName => "mollie";

    public Task<CheckoutSession> CreateAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie checkout implementation pending.");
}
