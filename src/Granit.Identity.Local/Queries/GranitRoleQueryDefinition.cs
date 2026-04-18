using Granit.Identity.Local.Domain;
using Granit.QueryEngine;

namespace Granit.Identity.Local.Queries;

/// <summary>
/// Query definition for identity roles — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class GranitRoleQueryDefinition : QueryDefinition<GranitRole>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Identity.GranitRoleQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<GranitRole> builder)
    {
        builder
            .Column(r => r.Name, c => c.Label("Name").LabelKey("Identity.Columns.RoleName").Filterable().Sortable())
            .Column(r => r.Description, c => c.Label("Description").LabelKey("Identity.Columns.Description").Filterable())
            .GlobalSearch(r => r.Name, r => r.Description)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
