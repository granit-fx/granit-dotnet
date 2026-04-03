namespace Granit.Tax;

/// <summary>
/// A tax rate for a specific country, valid over a date range.
/// </summary>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code.</param>
/// <param name="StandardRate">Standard VAT/tax rate (e.g., 0.21 for 21%).</param>
/// <param name="ReducedRate">Reduced rate, if applicable.</param>
/// <param name="SuperReducedRate">Super-reduced rate (Luxembourg, Spain, etc.).</param>
/// <param name="ParkingRate">Parking rate (transitional, few countries).</param>
/// <param name="EffectiveFrom">Start date of this rate.</param>
/// <param name="EffectiveTo">End date (null = current).</param>
public sealed record TaxRateEntry(
    string CountryCode,
    decimal StandardRate,
    decimal? ReducedRate = null,
    decimal? SuperReducedRate = null,
    decimal? ParkingRate = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null);
