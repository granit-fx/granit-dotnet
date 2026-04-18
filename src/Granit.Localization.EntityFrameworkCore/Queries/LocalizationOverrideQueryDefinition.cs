using Granit.Localization.EntityFrameworkCore.Entities;
using Granit.QueryEngine;

namespace Granit.Localization.EntityFrameworkCore.Queries;

/// <summary>
/// Query definition for localization overrides — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class LocalizationOverrideQueryDefinition : QueryDefinition<LocalizationOverride>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Localization.LocalizationOverrideQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<LocalizationOverride> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("Localization.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.ResourceName, c => c.Label("Resource").LabelKey("Localization.Columns.ResourceName").Filterable().Sortable())
            .Column(e => e.CultureName, c => c.Label("Culture").LabelKey("Localization.Columns.CultureName").Filterable().Sortable())
            .Column(e => e.Key, c => c.Label("Key").LabelKey("Localization.Columns.Key").Filterable().Sortable())
            .Column(e => e.Value, c => c.Label("Value").LabelKey("Localization.Columns.Value").Filterable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Localization.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("Localization.Columns.ModifiedAt").Sortable())
            .GlobalSearch(e => e.Key, e => e.Value, e => e.ResourceName)
            .DateFilter(e => e.ModifiedAt)
            .DefaultSort("-modifiedAt")
            .DefaultPageSize(25);
    }
}
