using Granit.QueryEngine;

namespace Granit.Tax.Queries;

/// <summary>
/// Query definition for effective tax rate entries resolved by
/// <see cref="ITaxRateProvider"/> (defaults + configuration + DB overrides).
/// </summary>
public sealed class TaxRateEntryQueryDefinition : QueryDefinition<TaxRateEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Tax.TaxRateEntryQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<TaxRateEntry> builder)
    {
        builder
            .Column(r => r.CountryCode, c => c.Label("Country Code").LabelKey("Tax.Columns.CountryCode").Filterable().Sortable())
            .Column(r => r.StandardRate, c => c.Label("Standard Rate").LabelKey("Tax.Columns.StandardRate").Sortable())
            .Column(r => r.ReducedRate, c => c.Label("Reduced Rate").LabelKey("Tax.Columns.ReducedRate").Sortable())
            .Column(r => r.SuperReducedRate, c => c.Label("Super-Reduced Rate").LabelKey("Tax.Columns.SuperReducedRate").Sortable())
            .Column(r => r.ParkingRate, c => c.Label("Parking Rate").LabelKey("Tax.Columns.ParkingRate").Sortable())
            .Column(r => r.EffectiveFrom, c => c.Label("Effective From").LabelKey("Tax.Columns.EffectiveFrom").Sortable())
            .Column(r => r.EffectiveTo, c => c.Label("Effective To").LabelKey("Tax.Columns.EffectiveTo").Sortable())
            .GlobalSearch(r => r.CountryCode)
            .DefaultSort("countryCode")
            .DefaultPageSize(50);
    }
}
