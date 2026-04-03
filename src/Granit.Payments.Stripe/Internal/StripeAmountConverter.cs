using System.Collections.Frozen;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Converts between Granit decimal amounts and Stripe integer amounts (smallest currency unit).
/// </summary>
/// <remarks>
/// Stripe represents amounts in the smallest currency unit (e.g., cents for EUR/USD).
/// Zero-decimal currencies (JPY, KRW, etc.) use the major unit directly.
/// </remarks>
internal static class StripeAmountConverter
{
    /// <summary>
    /// ISO 4217 zero-decimal currencies — amounts are in the major unit, not cents.
    /// Source: <see href="https://docs.stripe.com/currencies#zero-decimal"/>.
    /// </summary>
    private static readonly FrozenSet<string> ZeroDecimalCurrencies = FrozenSet.ToFrozenSet(
    [
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW",
        "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF",
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>Converts a decimal amount to Stripe's integer representation.</summary>
    public static long ToStripeAmount(decimal amount, string currency) =>
        IsZeroDecimal(currency)
            ? (long)amount
            : (long)(amount * 100m);

    /// <summary>Converts Stripe's integer amount back to a decimal.</summary>
    public static decimal FromStripeAmount(long amount, string currency) =>
        IsZeroDecimal(currency)
            ? amount
            : amount / 100m;

    private static bool IsZeroDecimal(string currency) =>
        ZeroDecimalCurrencies.Contains(currency);
}
