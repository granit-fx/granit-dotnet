using Granit.QueryEngine;
using Granit.Settings.Domain;

namespace Granit.Settings.Queries;

/// <summary>
/// Query definition for setting records — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class SettingRecordQueryDefinition : QueryDefinition<SettingRecord>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Settings.SettingRecordQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<SettingRecord> builder)
    {
        builder
            .Column(e => e.Name, c => c.Label("Name").LabelKey("Settings.Columns.Name").Filterable().Sortable())
            .Column(e => e.ProviderName, c => c.Label("Provider").LabelKey("Settings.Columns.ProviderName").Filterable().Sortable())
            .Column(e => e.ProviderKey, c => c.Label("Provider Key").LabelKey("Settings.Columns.ProviderKey").Filterable().Sortable())
            .Column(e => e.Value, c => c.Label("Value").LabelKey("Settings.Columns.Value").Filterable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Settings.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("Settings.Columns.ModifiedAt").Sortable())
            .GlobalSearch(e => e.Name, e => e.Value, e => e.ProviderKey)
            .DateFilter(e => e.ModifiedAt)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
