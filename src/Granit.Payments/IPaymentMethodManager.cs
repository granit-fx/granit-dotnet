using Granit.Payments.Contracts;

namespace Granit.Payments;

/// <summary>Manages saved payment methods (list, attach, detach) via provider.</summary>
public interface IPaymentMethodManager
{
    /// <summary>Provider name this manager serves.</summary>
    string ProviderName { get; }

    /// <summary>Lists saved payment methods for the given contact.</summary>
    Task<IReadOnlyList<PaymentProviderMethod>> ListAsync(Guid contactId, CancellationToken cancellationToken = default);

    /// <summary>Attaches a new payment method.</summary>
    Task<PaymentProviderMethod> AttachAsync(PaymentAttachMethodRequest request, CancellationToken cancellationToken = default);

    /// <summary>Detaches a saved payment method.</summary>
    Task DetachAsync(string providerMethodId, CancellationToken cancellationToken = default);
}
