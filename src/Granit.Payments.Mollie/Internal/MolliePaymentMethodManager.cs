using Granit.Payments.Contracts;

namespace Granit.Payments.Mollie.Internal;

internal sealed class MolliePaymentMethodManager : IPaymentMethodManager
{
    public string ProviderName => "mollie";

    public Task<IReadOnlyList<PaymentProviderMethod>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Mollie payment method management not configured.");

    public Task<PaymentProviderMethod> AttachAsync(PaymentAttachMethodRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Mollie payment method management not configured.");

    public Task DetachAsync(string providerMethodId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Mollie payment method management not configured.");
}
