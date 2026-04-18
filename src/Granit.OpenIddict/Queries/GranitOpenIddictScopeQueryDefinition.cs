using Granit.OpenIddict.Entities.OpenIddict;
using Granit.QueryEngine;

namespace Granit.OpenIddict.Queries;

/// <summary>
/// Query definition for OpenIddict scopes — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class GranitOpenIddictScopeQueryDefinition : QueryDefinition<GranitOpenIddictScope>
{
    /// <inheritdoc/>
    public override string Name => "Granit.OpenIddict.ScopeQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<GranitOpenIddictScope> builder)
    {
        builder
            .Column(s => s.TenantId, c => c.Label("Tenant").LabelKey("OpenIddict.Columns.Tenant").Filterable().Sortable())
            .Column(s => s.Name, c => c.Label("Name").LabelKey("OpenIddict.Columns.ScopeName").Filterable().Sortable())
            .Column(s => s.DisplayName, c => c.Label("Display Name").LabelKey("OpenIddict.Columns.DisplayName").Filterable().Sortable())
            .Column(s => s.Description, c => c.Label("Description").LabelKey("OpenIddict.Columns.Description").Filterable())
            .GlobalSearch(s => s.Name, s => s.DisplayName, s => s.Description)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
