using Granit.Parties.Domain;
using Granit.QueryEngine;

namespace Granit.Parties.Queries;

/// <summary>
/// Query definition for the contacts admin grid — declares columns, filters, sorting,
/// search, and pagination metadata for the query engine.
/// </summary>
public sealed class PartyQueryDefinition : QueryDefinition<Party>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Parties.PartyQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Party> builder)
    {
        builder
            .Column(c => c.Name, col => col.Label("Name").LabelKey("Parties.Columns.Name").Filterable().Sortable())
            .Column(c => c.Kind, col => col.Label("Kind").LabelKey("Parties.Columns.Kind").Filterable().Sortable())
            .Column(c => c.Roles, col => col.Label("Roles").LabelKey("Parties.Columns.Roles").Filterable())
            .Column(c => c.Status, col => col.Label("Status").LabelKey("Parties.Columns.Status").Filterable().Sortable())
            .Column(c => c.DefaultCurrency, col => col.Label("Currency").LabelKey("Parties.Columns.DefaultCurrency").Filterable().Sortable())
            .Column(c => c.Language, col => col.Label("Language").LabelKey("Parties.Columns.Language").Filterable())
            .Column(c => c.TaxId, col => col.Label("Tax ID").LabelKey("Parties.Columns.TaxId").Filterable())
            .Column(c => c.RegistrationNumber, col => col.Label("Registration #").LabelKey("Parties.Columns.RegistrationNumber").Filterable())
            .Column(c => c.CreatedAt, col => col.Label("Created").LabelKey("Parties.Columns.CreatedAt").Sortable())
            .Column(c => c.ModifiedAt, col => col.Label("Modified").LabelKey("Parties.Columns.ModifiedAt").Sortable())
            .GlobalSearch(c => c.Name, c => c.TaxId, c => c.RegistrationNumber)
            .DateFilter(c => c.CreatedAt)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
