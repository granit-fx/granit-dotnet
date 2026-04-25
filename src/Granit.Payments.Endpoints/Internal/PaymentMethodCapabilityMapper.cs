using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Dtos;

namespace Granit.Payments.Endpoints.Internal;

/// <summary>
/// Maps between <see cref="PaymentMethodCapability"/> (domain) and
/// <see cref="PaymentMethodCapabilityResponse"/> (API surface).
/// </summary>
internal static class PaymentMethodCapabilityMapper
{
    public static PaymentMethodCapabilityResponse ToResponse(PaymentMethodCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return new PaymentMethodCapabilityResponse(
            SupportedCountries: [.. capability.SupportedCountries.OrderBy(c => c, StringComparer.Ordinal)],
            SupportedCurrencies: [.. capability.SupportedCurrencies.OrderBy(c => c, StringComparer.Ordinal)],
            SupportedSequenceTypes: FlagsToNames(capability.SupportedSequenceTypes),
            AmountBounds:
            [
                .. capability.AmountBounds
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => new PaymentMethodAmountBoundResponse(
                        kv.Value.CurrencyCode, kv.Value.MinAmount, kv.Value.MaxAmount)),
            ]);
    }

    public static PaymentMethodCapabilityResponse? ToResponseOrNull(PaymentMethodCapability? capability) =>
        capability is null ? null : ToResponse(capability);

    private static List<string> FlagsToNames(PaymentMethodSequenceTypes flags)
    {
        List<string> names = new(3);

        if (flags.HasFlag(PaymentMethodSequenceTypes.OneOff))
        {
            names.Add("oneoff");
        }

        if (flags.HasFlag(PaymentMethodSequenceTypes.First))
        {
            names.Add("first");
        }

        if (flags.HasFlag(PaymentMethodSequenceTypes.Recurring))
        {
            names.Add("recurring");
        }

        return names;
    }
}
