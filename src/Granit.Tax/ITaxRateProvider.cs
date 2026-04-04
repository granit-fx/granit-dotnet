namespace Granit.Tax;

/// <summary>Provides tax rates by country and date.</summary>
public interface ITaxRateProvider
{
    /// <summary>Returns the tax rate for a country at a specific date.</summary>
    Task<TaxRateEntry?> GetRateAsync(
        string countryCode, DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all currently effective tax rates.</summary>
    Task<IReadOnlyList<TaxRateEntry>> GetAllCurrentRatesAsync(
        CancellationToken cancellationToken = default);
}
