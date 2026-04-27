using Granit.Parties.Domain.ValueObjects;

namespace Granit.Tax;

/// <summary>Provides tax rates by country, date, and (optionally) contact-specific status.</summary>
public interface ITaxRateProvider
{
    /// <summary>
    /// Returns the tax rate for a country at a specific date, optionally honouring the
    /// contact's <c>TaxStatus</c>. When <paramref name="contactId"/> is supplied and the
    /// contact's status yields a 0% rate (exempt or reverse-charge), the returned entry
    /// has <c>StandardRate = 0</c> and <c>ReducedRate = 0</c> — the country and effective
    /// dates remain populated so the caller still has the jurisdiction context for the
    /// document footer (e.g. "VAT reverse-charge per Art. 196").
    /// When <paramref name="contactId"/> is omitted (<c>null</c>), the legacy
    /// country-default behaviour applies — preserves binary compatibility with consumers
    /// that have not migrated to PartyId yet.
    /// </summary>
    Task<TaxRateEntry?> GetRateAsync(
        string countryCode,
        DateTimeOffset asOf,
        PartyId? contactId = null,
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
