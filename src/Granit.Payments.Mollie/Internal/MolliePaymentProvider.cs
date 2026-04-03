using Granit.Payments.Dtos;

namespace Granit.Payments.Mollie.Internal;

/// <summary>Mollie implementation of IPaymentProvider.</summary>
internal sealed class MolliePaymentProvider : IPaymentProvider
{
    public string Name => "mollie";

    public Task<ProviderChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie provider implementation pending.");

    public Task<ProviderRefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie provider implementation pending.");

    public Task<ProviderPaymentStatus> GetStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie provider implementation pending.");
}
