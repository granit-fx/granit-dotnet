using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments.Mollie.Internal;

/// <summary>Mollie implementation of IPaymentProvider.</summary>
internal sealed class MolliePaymentProvider : IPaymentProvider
{
    public string Name => "mollie";

    public Task<PaymentProviderChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentProviderChargeResult(
            ProviderTransactionId: string.Empty, Status: ProviderChargeStatus.Failed));

    public Task<PaymentProviderRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentProviderRefundResult(
            ProviderRefundId: string.Empty, Status: RefundStatus.Failed));

    public Task<PaymentProviderStatus> GetStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentProviderStatus(
            ProviderTransactionId: providerTransactionId, Status: PaymentStatus.Failed));
}
