using Granit.DataExchange.Import.Domain;
using Granit.QueryEngine;

namespace Granit.DataExchange.Queries;

/// <summary>
/// Query definition for import jobs — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class ImportJobQueryDefinition : QueryDefinition<ImportJob>
{
    /// <inheritdoc/>
    public override string Name => "Granit.DataExchange.ImportJobQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<ImportJob> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("DataExchange.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.DefinitionName, c => c.Label("Definition").LabelKey("DataExchange.Columns.DefinitionName").Filterable().Sortable())
            .Column(e => e.EntityTypeName, c => c.Label("Entity Type").LabelKey("DataExchange.Columns.EntityTypeName").Filterable().Sortable())
            .Column(e => e.OriginalFileName, c => c.Label("File Name").LabelKey("DataExchange.Columns.OriginalFileName").Filterable().Sortable())
            .Column(e => e.Status, c => c.Label("Status").LabelKey("DataExchange.Columns.Status").Filterable().Sortable())
            .Column(e => e.FileSizeBytes, c => c.Label("Size (bytes)").LabelKey("DataExchange.Columns.FileSizeBytes").Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("DataExchange.Columns.CreatedAt").Sortable())
            .Column(e => e.CompletedAt, c => c.Label("Completed At").LabelKey("DataExchange.Columns.CompletedAt").Sortable())
            .AllowGroupBy(e => e.Status)
            .GlobalSearch(e => e.OriginalFileName, e => e.DefinitionName, e => e.EntityTypeName)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
