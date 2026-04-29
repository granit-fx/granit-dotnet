using Granit.Parties.Domain;
using Granit.QueryEngine;

namespace Granit.Parties.Queries;

/// <summary>
/// Query definition for the parties admin grid — declares columns, filters, sorting,
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
            // Identity
            .Column(c => c.TenantId, col => col.Label("Tenant").LabelKey("Parties.Columns.Tenant").Filterable().Sortable())
            .Column(c => c.Name, col => col.Label("Name").LabelKey("Parties.Columns.Name").Filterable().Sortable())
            .Column(c => c.Kind, col => col.Label("Kind").LabelKey("Parties.Columns.Kind").Filterable().Sortable())
            .Column(c => c.Roles, col => col.Label("Roles").LabelKey("Parties.Columns.Roles").Filterable())
            .Column(c => c.Status, col => col.Label("Status").LabelKey("Parties.Columns.Status").Filterable().Sortable())
            // Contact / branding
            .Column(c => c.Website, col => col.Label("Website").LabelKey("Parties.Columns.Website").Filterable())
            .Column(c => c.AvatarBlobId, col => col.Label("Avatar").LabelKey("Parties.Columns.Avatar").Filterable())
            // Locale
            .Column(c => c.DefaultCurrency, col => col.Label("Currency").LabelKey("Parties.Columns.DefaultCurrency").Filterable().Sortable())
            .Column(c => c.Language, col => col.Label("Language").LabelKey("Parties.Columns.Language").Filterable())
            .Column(c => c.Timezone, col => col.Label("Timezone").LabelKey("Parties.Columns.Timezone").Filterable().Sortable())
            // Tax & registration
            .Column(c => c.TaxId, col => col.Label("Tax ID").LabelKey("Parties.Columns.TaxId").Filterable())
            .Column(c => c.RegistrationNumber, col => col.Label("Registration #").LabelKey("Parties.Columns.RegistrationNumber").Filterable())
            .Column(c => c.TaxStatus, col => col.Label("Tax Status").LabelKey("Parties.Columns.TaxStatus").Filterable().Sortable())
            // Relationships
            .Column(c => c.ParentPartyId, col => col.Label("Parent Party").LabelKey("Parties.Columns.ParentParty").Filterable())
            .Column(c => c.UserId, col => col.Label("User").LabelKey("Parties.Columns.User").Filterable())
            // Merge lifecycle
            .Column(c => c.MergedIntoId, col => col.Label("Merged Into").LabelKey("Parties.Columns.MergedInto").Filterable())
            .Column(c => c.MergedAt, col => col.Label("Merged At").LabelKey("Parties.Columns.MergedAt").Sortable())
            // Audit
            .Column(c => c.CreatedAt, col => col.Label("Created").LabelKey("Parties.Columns.CreatedAt").Sortable())
            .Column(c => c.ModifiedAt, col => col.Label("Modified").LabelKey("Parties.Columns.ModifiedAt").Sortable())
            .GlobalSearch(c => c.Name, c => c.TaxId, c => c.RegistrationNumber)
            .DateFilter(c => c.CreatedAt)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
