using System.Globalization;
using System.Text.RegularExpressions;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments.Endpoints.Internal;

/// <summary>
/// Parses and validates the <c>GET /methods/available</c> query parameters into a
/// <see cref="PaymentAvailabilityContext"/>.
/// </summary>
internal static partial class PaymentAvailabilityContextParser
{
    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant, 50)]
    private static partial Regex CountryRegex();

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, 50)]
    private static partial Regex CurrencyRegex();

    /// <summary>
    /// Builds a <see cref="PaymentAvailabilityContext"/> from raw query values, returning
    /// <see langword="null"/> when no axis is set. Writes a human-readable reason into
    /// <paramref name="error"/> when any axis is malformed.
    /// </summary>
    public static PaymentAvailabilityContext? TryParse(
        string? country,
        string? currency,
        decimal? amount,
        string? sequenceType,
        out string? error)
    {
        error = null;

        string? normalizedCountry = null;
        if (!string.IsNullOrWhiteSpace(country))
        {
            normalizedCountry = country.Trim().ToUpperInvariant();
            if (!CountryRegex().IsMatch(normalizedCountry))
            {
                error = $"'{country}' is not a valid ISO-3166 alpha-2 country code.";
                return null;
            }
        }

        string? normalizedCurrency = null;
        if (!string.IsNullOrWhiteSpace(currency))
        {
            normalizedCurrency = currency.Trim().ToUpperInvariant();
            if (!CurrencyRegex().IsMatch(normalizedCurrency))
            {
                error = $"'{currency}' is not a valid ISO-4217 alpha-3 currency code.";
                return null;
            }
        }

        if (amount is < 0m)
        {
            error = $"amount must be non-negative (received {amount.Value.ToString(CultureInfo.InvariantCulture)}).";
            return null;
        }

        PaymentMethodSequenceType sequence = PaymentMethodSequenceType.OneOff;
        if (!string.IsNullOrWhiteSpace(sequenceType))
        {
            sequence = sequenceType.Trim().ToLowerInvariant() switch
            {
                "oneoff" => PaymentMethodSequenceType.OneOff,
                "first" => PaymentMethodSequenceType.First,
                "recurring" => PaymentMethodSequenceType.Recurring,
                _ => PaymentMethodSequenceType.None,
            };

            if (sequence == PaymentMethodSequenceType.None)
            {
                error = $"'{sequenceType}' is not a valid sequence type (expected: oneoff, first, recurring).";
                return null;
            }
        }

        if (normalizedCountry is null && normalizedCurrency is null && amount is null
            && string.IsNullOrWhiteSpace(sequenceType))
        {
            return null;
        }

        return new PaymentAvailabilityContext(normalizedCountry, normalizedCurrency, amount, sequence);
    }
}
