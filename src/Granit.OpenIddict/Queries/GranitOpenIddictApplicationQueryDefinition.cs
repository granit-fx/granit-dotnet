using Granit.OpenIddict.Models;
using Granit.QueryEngine;

namespace Granit.OpenIddict.Queries;

/// <summary>
/// Query definition for OpenIddict applications — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class GranitOpenIddictApplicationQueryDefinition : QueryDefinition<OpenIddictApplicationModel>
{
    /// <inheritdoc/>
    public override string Name => "Granit.OpenIddict.ApplicationQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<OpenIddictApplicationModel> builder)
    {
        builder
            .Column(a => a.TenantId, c => c.Label("Tenant").LabelKey("OpenIddict.Columns.Tenant").Filterable().Sortable())
            .Column(a => a.ClientId, c => c.Label("Client ID").LabelKey("OpenIddict.Columns.ClientId").Filterable().Sortable())
            .Column(a => a.DisplayName, c => c.Label("Display Name").LabelKey("OpenIddict.Columns.DisplayName").Filterable().Sortable())
            .Column(a => a.ClientType, c => c.Label("Client Type").LabelKey("OpenIddict.Columns.ClientType").Filterable().Sortable())
            .Column(a => a.ConsentType, c => c.Label("Consent Type").LabelKey("OpenIddict.Columns.ConsentType").Filterable().Sortable())
            .Column(a => a.ApplicationType, c => c.Label("Application Type").LabelKey("OpenIddict.Columns.ApplicationType").Filterable().Sortable())
            .GlobalSearch(a => a.ClientId, a => a.DisplayName)
            .DefaultSort("clientId")
            .DefaultPageSize(25);
    }
}
