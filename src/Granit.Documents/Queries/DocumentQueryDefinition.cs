using Granit.Documents.Domain;
using Granit.Documents.Internal;
using Granit.QueryEngine;

namespace Granit.Documents.Queries;

/// <summary>
/// Query definition for the documents admin grid — declares columns, filters,
/// sorting, search, and pagination metadata for the query engine (F11.1).
/// </summary>
public sealed class DocumentQueryDefinition : QueryDefinition<Document>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.DocumentQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(DocumentsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Document> builder)
    {
        builder
            .Column(d => d.TenantId, c => c.Label("Tenant").LabelKey("Documents.Columns.Tenant").Filterable().Sortable())
            .Column(d => d.FolderId, c => c.Label("Folder").LabelKey("Documents.Columns.Folder").Filterable().Sortable())
            .Column(d => d.OwnerUserId, c => c.Label("Owner").LabelKey("Documents.Columns.Owner").Filterable().Sortable())
            .Column(d => d.Name, c => c.Label("Name").LabelKey("Documents.Columns.Name").Filterable().Sortable())
            .Column(d => d.Description, c => c.Label("Description").LabelKey("Documents.Columns.Description").Filterable())
            .Column(d => d.Status, c => c.Label("Status").LabelKey("Documents.Columns.Status").Filterable().Sortable())
            .Column(d => d.CurrentVersionId, c => c.Label("Current Version").LabelKey("Documents.Columns.CurrentVersion"))
            .Column(d => d.TrashedAt, c => c.Label("Trashed At").LabelKey("Documents.Columns.TrashedAt").Sortable())
            .GlobalSearch(d => d.Name, d => d.Description)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
