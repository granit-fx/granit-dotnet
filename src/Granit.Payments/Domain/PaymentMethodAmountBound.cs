namespace Granit.Payments.Domain;

/// <summary>
/// Amount bounds declared by a payment method for a specific currency.
/// </summary>
/// <param name="CurrencyCode">ISO-4217 alpha-3 code, upper-case (e.g., <c>EUR</c>).</param>
/// <param name="MinAmount">Minimum accepted amount, or <see langword="null"/> for no floor.</param>
/// <param name="MaxAmount">Maximum accepted amount, or <see langword="null"/> for no ceiling.</param>
public sealed record PaymentMethodAmountBound(
    string CurrencyCode,
    decimal? MinAmount,
    decimal? MaxAmount);
