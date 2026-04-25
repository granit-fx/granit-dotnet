using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments.Internal;

/// <summary>
/// Default <see cref="IPaymentMethodAvailabilityFilter"/>. Intersects a capability with a
/// request context across four axes: country, currency, amount, and sequence type.
/// </summary>
/// <remarks>
/// Empty sets / empty bounds dictionaries are treated as wildcards. Null axes on the
/// context are not filtered.
///
/// <para>
/// Sequence type matching uses a bitwise containment test:
/// <c>(capability.SupportedSequenceTypes &amp; context.SequenceType) == context.SequenceType</c>.
/// A caller requesting <see cref="PaymentMethodSequenceTypes.Recurring"/> against a method
/// that supports only <see cref="PaymentMethodSequenceTypes.OneOff"/> is rejected; the filter
/// does not infer mandate-setup chains.
/// </para>
/// </remarks>
internal sealed class DefaultPaymentMethodAvailabilityFilter : IPaymentMethodAvailabilityFilter
{
    public bool IsAvailable(PaymentMethodCapability capability, PaymentAvailabilityContext context)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(context);

        if (context.CountryCode is { Length: > 0 } country
            && capability.SupportedCountries.Count > 0
            && !capability.SupportedCountries.Contains(country))
        {
            return false;
        }

        if (context.CurrencyCode is { Length: > 0 } currency
            && capability.SupportedCurrencies.Count > 0
            && !capability.SupportedCurrencies.Contains(currency))
        {
            return false;
        }

        if (context.SequenceType != PaymentMethodSequenceTypes.None
            && (capability.SupportedSequenceTypes & context.SequenceType) != context.SequenceType)
        {
            return false;
        }

        if (context.Amount is { } amount
            && context.CurrencyCode is { Length: > 0 } boundsCurrency
            && capability.AmountBounds.TryGetValue(boundsCurrency, out PaymentMethodAmountBound? bound))
        {
            if (bound.MinAmount is { } min && amount < min)
            {
                return false;
            }

            if (bound.MaxAmount is { } max && amount > max)
            {
                return false;
            }
        }

        return true;
    }
}
