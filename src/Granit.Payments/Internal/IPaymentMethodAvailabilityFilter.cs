using Granit.Payments.Contracts;

namespace Granit.Payments.Internal;

/// <summary>
/// Pure predicate evaluating whether a <see cref="PaymentMethodCapability"/> satisfies
/// a <see cref="PaymentAvailabilityContext"/>.
/// </summary>
internal interface IPaymentMethodAvailabilityFilter
{
    /// <summary>
    /// Returns <see langword="true"/> when every non-null axis of <paramref name="context"/>
    /// is compatible with <paramref name="capability"/>.
    /// </summary>
    bool IsAvailable(PaymentMethodCapability capability, PaymentAvailabilityContext context);
}
