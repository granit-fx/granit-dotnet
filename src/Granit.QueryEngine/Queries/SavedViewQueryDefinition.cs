using Granit.QueryEngine.SavedViews.Domain;

namespace Granit.QueryEngine.Queries;

/// <summary>
/// Query definition for saved views — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class SavedViewQueryDefinition : QueryDefinition<SavedView>
{
    /// <inheritdoc/>
    public override string Name => "Granit.QueryEngine.SavedViewQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<SavedView> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("QueryEngine.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.EntityType, c => c.Label("Entity Type").LabelKey("QueryEngine.Columns.EntityType").Filterable().Sortable())
            .Column(e => e.Name, c => c.Label("Name").LabelKey("QueryEngine.Columns.Name").Filterable().Sortable())
            .Column(e => e.UserId, c => c.Label("User").LabelKey("QueryEngine.Columns.UserId").Filterable().Sortable())
            .Column(e => e.IsShared, c => c.Label("Shared").LabelKey("QueryEngine.Columns.IsShared").Filterable().Sortable())
            .Column(e => e.IsDefault, c => c.Label("Default").LabelKey("QueryEngine.Columns.IsDefault").Filterable().Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("QueryEngine.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("QueryEngine.Columns.ModifiedAt").Sortable())
            .GlobalSearch(e => e.Name, e => e.EntityType)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
