using Granit.Payments.Contracts;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Mollie payment method management.
/// </summary>
/// <remarks>
/// Mollie does not support saved payment methods server-side in the same way as Stripe.
/// Customers select their method on the hosted payment page. This manager returns an
/// empty list and throws on attach/detach — by design.
/// </remarks>
internal sealed class MolliePaymentMethodManager : IPaymentMethodManager
{
    /// <inheritdoc/>
    public string ProviderName => "mollie";

    /// <inheritdoc/>
    public Task<IReadOnlyList<PaymentProviderMethod>> ListAsync(
        Guid partyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentProviderMethod>>([]);

    /// <inheritdoc/>
    public Task<PaymentProviderMethod> AttachAsync(
        PaymentAttachMethodRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Mollie uses hosted payment pages — payment methods are selected during checkout, not stored server-side.");

    /// <inheritdoc/>
    public Task DetachAsync(
        string providerMethodId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Mollie does not support server-side payment method management.");
}
