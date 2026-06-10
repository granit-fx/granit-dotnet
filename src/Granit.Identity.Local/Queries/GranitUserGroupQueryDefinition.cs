using Granit.Identity.Local.Domain;
using Granit.QueryEngine;

namespace Granit.Identity.Local.Queries;

/// <summary>
/// Query definition for identity user groups — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class GranitUserGroupQueryDefinition : QueryDefinition<GranitUserGroup>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Identity.Local.GranitUserGroupQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<GranitUserGroup> builder)
    {
        builder
            .Column(g => g.TenantId, c => c.Label("Tenant").LabelKey("Identity.Columns.Tenant").Filterable().Sortable())
            .Column(g => g.Name, c => c.Label("Name").LabelKey("Identity.Columns.GroupName").Filterable().Sortable())
            .Column(g => g.Description, c => c.Label("Description").LabelKey("Identity.Columns.Description").Filterable())
            .Column(g => g.CreatedAt, c => c.Label("Created At").LabelKey("Identity.Columns.CreatedAt").Sortable())
            .Column(g => g.ModifiedAt, c => c.Label("Modified At").LabelKey("Identity.Columns.ModifiedAt").Sortable())
            .GlobalSearch(g => g.Name, g => g.Description)
            .DateFilter(g => g.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
