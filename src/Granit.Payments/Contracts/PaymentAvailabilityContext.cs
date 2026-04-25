using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>
/// Request context used to filter available payment methods at checkout.
/// </summary>
/// <remarks>
/// Any axis set to <see langword="null"/> is treated as a wildcard on that axis (no filter).
/// </remarks>
/// <param name="CountryCode">ISO-3166 alpha-2 code (upper-case), typically the billing country.</param>
/// <param name="CurrencyCode">ISO-4217 alpha-3 code (upper-case), the transaction currency.</param>
/// <param name="Amount">Transaction amount in <paramref name="CurrencyCode"/>.</param>
/// <param name="SequenceType">Intended sequence mode for this transaction.</param>
public sealed record PaymentAvailabilityContext(
    string? CountryCode,
    string? CurrencyCode,
    decimal? Amount,
    PaymentMethodSequenceTypes SequenceType = PaymentMethodSequenceTypes.OneOff);
