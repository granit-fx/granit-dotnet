using Granit.Tax.Domain;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Tax.EntityFrameworkCore.Internal;

/// <summary>
/// DB-backed tax rate provider. Checks <see cref="TaxRateOverride"/> first,
/// delegates to the fallback (config-based) provider if no override exists.
/// </summary>
internal sealed class EfTaxRateProvider(
    IDbContextFactory<TaxDbContext> contextFactory,
    ITaxRateProvider fallback,
    IClock clock) : ITaxRateProvider
{
    public async Task<TaxRateEntry?> GetRateAsync(
        string countryCode, DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        await using TaxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TaxRateOverride? dbOverride = await db.TaxRateOverrides
            .Where(r => r.CountryCode == countryCode
                && r.EffectiveFrom <= asOf
                && (r.EffectiveTo == null || r.EffectiveTo > asOf))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dbOverride is not null)
        {
            return new TaxRateEntry(
                dbOverride.CountryCode,
                dbOverride.StandardRate,
                dbOverride.ReducedRate,
                EffectiveFrom: dbOverride.EffectiveFrom,
                EffectiveTo: dbOverride.EffectiveTo);
        }

        return await fallback.GetRateAsync(countryCode, asOf, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaxRateEntry>> GetAllCurrentRatesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TaxRateEntry> baseRates = await fallback
            .GetAllCurrentRatesAsync(cancellationToken)
            .ConfigureAwait(false);

        await using TaxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        List<TaxRateOverride> overrides = await db.TaxRateOverrides
            .Where(r => r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Merge(baseRates, overrides);
    }

    public IReadOnlyList<TaxRateEntry> GetAllCurrentRates()
    {
        IReadOnlyList<TaxRateEntry> baseRates = fallback.GetAllCurrentRates();

        using TaxDbContext db = contextFactory.CreateDbContext();

        DateTimeOffset now = clock.Now;
        var overrides = db.TaxRateOverrides
            .Where(r => r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now))
            .ToList();

        return Merge(baseRates, overrides);
    }

    private static List<TaxRateEntry> Merge(
        IReadOnlyList<TaxRateEntry> baseRates,
        List<TaxRateOverride> overrides)
    {
        // DB overrides take precedence over the fallback rates.
        var overrideMap = overrides.ToDictionary(
            r => r.CountryCode, r => r, StringComparer.OrdinalIgnoreCase);

        return baseRates.Select(rate =>
            overrideMap.TryGetValue(rate.CountryCode, out TaxRateOverride? dbRate)
                ? rate with { StandardRate = dbRate.StandardRate, ReducedRate = dbRate.ReducedRate }
                : rate
        ).ToList();
    }
}
