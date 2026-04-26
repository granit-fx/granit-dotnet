using Granit.Contacts.Domain;
using Granit.QueryEngine;

namespace Granit.Contacts.Queries;

/// <summary>
/// Query definition for the contacts admin grid — declares columns, filters, sorting,
/// search, and pagination metadata for the query engine.
/// </summary>
public sealed class ContactQueryDefinition : QueryDefinition<Contact>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Contacts.ContactQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Contact> builder)
    {
        builder
            .Column(c => c.Name, col => col.Label("Name").LabelKey("Contacts.Columns.Name").Filterable().Sortable())
            .Column(c => c.Kind, col => col.Label("Kind").LabelKey("Contacts.Columns.Kind").Filterable().Sortable())
            .Column(c => c.Roles, col => col.Label("Roles").LabelKey("Contacts.Columns.Roles").Filterable())
            .Column(c => c.Status, col => col.Label("Status").LabelKey("Contacts.Columns.Status").Filterable().Sortable())
            .Column(c => c.DefaultCurrency, col => col.Label("Currency").LabelKey("Contacts.Columns.DefaultCurrency").Filterable().Sortable())
            .Column(c => c.Language, col => col.Label("Language").LabelKey("Contacts.Columns.Language").Filterable())
            .Column(c => c.TaxId, col => col.Label("Tax ID").LabelKey("Contacts.Columns.TaxId").Filterable())
            .Column(c => c.RegistrationNumber, col => col.Label("Registration #").LabelKey("Contacts.Columns.RegistrationNumber").Filterable())
            .Column(c => c.CreatedAt, col => col.Label("Created").LabelKey("Contacts.Columns.CreatedAt").Sortable())
            .Column(c => c.ModifiedAt, col => col.Label("Modified").LabelKey("Contacts.Columns.ModifiedAt").Sortable())
            .GlobalSearch(c => c.Name, c => c.TaxId, c => c.RegistrationNumber)
            .DateFilter(c => c.CreatedAt)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
