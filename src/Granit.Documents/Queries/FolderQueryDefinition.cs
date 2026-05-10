using Granit.Documents.Domain;
using Granit.Documents.Internal;
using Granit.QueryEngine;

namespace Granit.Documents.Queries;

/// <summary>
/// Query definition for the folders admin grid (F11.2).
/// </summary>
public sealed class FolderQueryDefinition : QueryDefinition<Folder>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.FolderQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(DocumentsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Folder> builder)
    {
        builder
            .Column(f => f.TenantId, c => c.Label("Tenant").LabelKey("Documents.Columns.Folder.Tenant").Filterable().Sortable())
            .Column(f => f.ParentFolderId, c => c.Label("Parent Folder").LabelKey("Documents.Columns.Folder.Parent").Filterable())
            .Column(f => f.Name, c => c.Label("Name").LabelKey("Documents.Columns.Folder.Name").Filterable().Sortable())
            .Column(f => f.Path, c => c.Label("Path").LabelKey("Documents.Columns.Folder.Path").Filterable().Sortable())
            .Column(f => f.Depth, c => c.Label("Depth").LabelKey("Documents.Columns.Folder.Depth").Filterable().Sortable())
            .Column(f => f.OwnerUserId, c => c.Label("Owner").LabelKey("Documents.Columns.Folder.Owner").Filterable())
            .Column(f => f.IsTenantRoot, c => c.Label("Is Tenant Root").LabelKey("Documents.Columns.Folder.IsTenantRoot").Filterable())
            .Column(f => f.Status, c => c.Label("Status").LabelKey("Documents.Columns.Folder.Status").Filterable().Sortable())
            .Column(f => f.TrashedAt, c => c.Label("Trashed At").LabelKey("Documents.Columns.Folder.TrashedAt").Sortable())
            .GlobalSearch(f => f.Name, f => f.Path)
            .DefaultSort("path")
            .DefaultPageSize(25);
    }
}
