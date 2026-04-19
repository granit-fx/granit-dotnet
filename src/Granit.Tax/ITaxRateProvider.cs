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

    /// <summary>
    /// Synchronous variant of <see cref="GetAllCurrentRatesAsync"/>.
    /// Required by <c>IQueryableSource&lt;TaxRateEntry&gt;</c> so the query engine
    /// can expose the resolved rates via <c>MapGranitQuery&lt;TaxRateEntry&gt;()</c>.
    /// Implementations must avoid blocking the calling thread on async I/O.
    /// </summary>
    IReadOnlyList<TaxRateEntry> GetAllCurrentRates();
}
