using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>
/// Availability metadata declared by a payment method.
/// </summary>
/// <remarks>
/// Empty <see cref="SupportedCountries"/> or <see cref="SupportedCurrencies"/> means
/// <em>wildcard</em> (no constraint on that axis). An empty <see cref="AmountBounds"/>
/// dictionary means the method has no per-currency bounds (e.g., cards, wallets).
/// </remarks>
/// <param name="SupportedCountries">ISO-3166 alpha-2 codes (upper-case). Empty = global.</param>
/// <param name="SupportedCurrencies">ISO-4217 alpha-3 codes (upper-case). Empty = all.</param>
/// <param name="SupportedSequenceTypes">Supported sequence modes (flags). Must include at least one mode.</param>
/// <param name="AmountBounds">Per-currency amount bounds. Keys are ISO-4217 upper-case.</param>
public sealed record PaymentMethodCapability(
    IReadOnlySet<string> SupportedCountries,
    IReadOnlySet<string> SupportedCurrencies,
    PaymentMethodSequenceType SupportedSequenceTypes,
    IReadOnlyDictionary<string, PaymentMethodAmountBound> AmountBounds);
