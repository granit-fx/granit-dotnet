using Granit.QueryEngine;

namespace Granit.Tax.Internal;

/// <summary>
/// In-memory <see cref="IQueryableSource{TEntity}"/> for <see cref="TaxRateEntry"/>.
/// Wraps <see cref="ITaxRateProvider.GetAllCurrentRates"/> so the query engine can
/// filter, sort and page the resolved rates without forcing a sync-over-async hop.
/// </summary>
internal sealed class TaxRateEntryQueryableSource(
    ITaxRateProvider provider) : IQueryableSource<TaxRateEntry>
{
    public IQueryable<TaxRateEntry> GetQueryable() =>
        provider.GetAllCurrentRates().AsQueryable();
}
