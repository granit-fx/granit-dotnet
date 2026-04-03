using Granit.Payments.Dtos;

namespace Granit.Payments.Mollie.Internal;

internal sealed class MolliePaymentMethodManager : IPaymentMethodManager
{
    public string ProviderName => "mollie";

    public Task<IReadOnlyList<ProviderPaymentMethod>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie payment method implementation pending.");

    public Task<ProviderPaymentMethod> AttachAsync(AttachPaymentMethodRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie payment method implementation pending.");

    public Task DetachAsync(string providerMethodId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie payment method implementation pending.");
}
