using Granit.Payments.Dtos;

namespace Granit.Payments;

/// <summary>Manages saved payment methods (list, attach, detach) via provider.</summary>
public interface IPaymentMethodManager
{
    /// <summary>Provider name this manager serves.</summary>
    string ProviderName { get; }

    /// <summary>Lists saved payment methods for a tenant.</summary>
    Task<IReadOnlyList<ProviderPaymentMethod>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Attaches a new payment method.</summary>
    Task<ProviderPaymentMethod> AttachAsync(AttachPaymentMethodRequest request, CancellationToken cancellationToken = default);

    /// <summary>Detaches a saved payment method.</summary>
    Task DetachAsync(string providerMethodId, CancellationToken cancellationToken = default);
}
