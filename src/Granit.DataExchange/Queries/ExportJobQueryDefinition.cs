using Granit.DataExchange.Export.Domain;
using Granit.QueryEngine;

namespace Granit.DataExchange.Queries;

/// <summary>
/// Query definition for export jobs — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class ExportJobQueryDefinition : QueryDefinition<ExportJob>
{
    /// <inheritdoc/>
    public override string Name => "Granit.DataExchange.ExportJobQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<ExportJob> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("DataExchange.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.DefinitionName, c => c.Label("Definition").LabelKey("DataExchange.Columns.DefinitionName").Filterable().Sortable())
            .Column(e => e.Format, c => c.Label("Format").LabelKey("DataExchange.Columns.Format").Filterable().Sortable())
            .Column(e => e.Status, c => c.Label("Status").LabelKey("DataExchange.Columns.Status").Filterable().Sortable())
            .Column(e => e.FileName, c => c.Label("File Name").LabelKey("DataExchange.Columns.FileName").Filterable().Sortable())
            .Column(e => e.RowCount, c => c.Label("Row Count").LabelKey("DataExchange.Columns.RowCount").Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("DataExchange.Columns.CreatedAt").Sortable())
            .Column(e => e.CompletedAt, c => c.Label("Completed At").LabelKey("DataExchange.Columns.CompletedAt").Sortable())
            .GlobalSearch(e => e.FileName, e => e.DefinitionName)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
