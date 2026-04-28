using Granit.Parties.Domain.ValueObjects;
using Granit.Tax.Builtin.Options;
using Microsoft.Extensions.Options;

namespace Granit.Tax.Builtin.Internal;

/// <summary>
/// Config-based tax rate provider. Reads from <see cref="EuVatRateOptions"/>
/// and falls back to <see cref="EuVatRateDefaults"/> for unconfigured countries.
/// Customer-specific tax overrides (exempt, reverse-charge) are honoured by the
/// upstream <c>EfTaxRateProvider</c> — this provider is the country-only fallback.
/// </summary>
internal sealed class ConfigTaxRateProvider(
    IOptions<EuVatRateOptions> options) : ITaxRateProvider
{
    public Task<TaxRateEntry?> GetRateAsync(
        string countryCode,
        DateTimeOffset asOf,
        PartyId? partyId = null,
        CancellationToken cancellationToken = default)
    {
        // PartyId is honoured by the upstream EfTaxRateProvider; this provider
        // returns the country default only.
        _ = partyId;

        string normalized = countryCode.ToUpperInvariant();

        // Check configured overrides first
        if (options.Value.StandardRates.TryGetValue(normalized, out decimal configRate))
        {
            return Task.FromResult<TaxRateEntry?>(new TaxRateEntry(
                normalized, configRate,
                options.Value.ReducedRates?.GetValueOrDefault(normalized)));
        }

        // Fall back to defaults
        if (EuVatRateDefaults.StandardRates.TryGetValue(normalized, out decimal defaultRate))
        {
            return Task.FromResult<TaxRateEntry?>(new TaxRateEntry(normalized, defaultRate));
        }

        return Task.FromResult<TaxRateEntry?>(null);
    }

    public Task<IReadOnlyList<TaxRateEntry>> GetAllCurrentRatesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetAllCurrentRates());

    public IReadOnlyList<TaxRateEntry> GetAllCurrentRates()
    {
        var rates = new List<TaxRateEntry>(EuVatRateDefaults.StandardRates.Count);

        foreach (KeyValuePair<string, decimal> entry in EuVatRateDefaults.StandardRates)
        {
            decimal rate = options.Value.StandardRates.TryGetValue(entry.Key, out decimal configRate)
                ? configRate
                : entry.Value;

            rates.Add(new TaxRateEntry(
                entry.Key, rate,
                options.Value.ReducedRates?.GetValueOrDefault(entry.Key)));
        }

        return rates;
    }
}
