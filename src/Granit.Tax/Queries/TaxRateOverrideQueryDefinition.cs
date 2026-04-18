using Granit.QueryEngine;
using Granit.Tax.Domain;

namespace Granit.Tax.Queries;

/// <summary>
/// Query definition for tax rate overrides — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class TaxRateOverrideQueryDefinition : QueryDefinition<TaxRateOverride>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Tax.TaxRateOverrideQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<TaxRateOverride> builder)
    {
        builder
            .Column(t => t.TenantId, c => c.Label("Tenant").LabelKey("Tax.Columns.Tenant").Filterable().Sortable())
            .Column(t => t.CountryCode, c => c.Label("Country Code").LabelKey("Tax.Columns.CountryCode").Filterable().Sortable())
            .Column(t => t.StandardRate, c => c.Label("Standard Rate").LabelKey("Tax.Columns.StandardRate").Sortable())
            .Column(t => t.ReducedRate, c => c.Label("Reduced Rate").LabelKey("Tax.Columns.ReducedRate").Sortable())
            .Column(t => t.EffectiveFrom, c => c.Label("Effective From").LabelKey("Tax.Columns.EffectiveFrom").Sortable())
            .Column(t => t.EffectiveTo, c => c.Label("Effective To").LabelKey("Tax.Columns.EffectiveTo").Sortable())
            .GlobalSearch(t => t.CountryCode)
            .DateFilter(t => t.EffectiveFrom)
            .DefaultSort("-effectiveFrom")
            .DefaultPageSize(25);
    }
}
