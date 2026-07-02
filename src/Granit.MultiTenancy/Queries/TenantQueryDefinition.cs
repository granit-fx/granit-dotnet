using Granit.MultiTenancy.Domain;
using Granit.QueryEngine;

namespace Granit.MultiTenancy.Queries;

/// <summary>
/// Query definition for tenants — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class TenantQueryDefinition : QueryDefinition<Tenant>
{
    /// <inheritdoc/>
    public override string Name => "Granit.MultiTenancy.TenantQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Tenant> builder)
    {
        builder
            .Column(t => t.Name, c => c.Label("Name").LabelKey("MultiTenancy.Columns.Name").Filterable().Sortable())
            .Column(t => t.Identifier, c => c.Label("Identifier").LabelKey("MultiTenancy.Columns.Identifier").Filterable().Sortable())
            .Column(t => t.ContactEmail, c => c.Label("Contact Email").LabelKey("MultiTenancy.Columns.ContactEmail").Filterable())
            .Column(t => t.Jurisdiction, c => c.Label("Jurisdiction").LabelKey("MultiTenancy.Columns.Jurisdiction").Filterable())
            .Column(t => t.Activated, c => c.Label("Activated").LabelKey("MultiTenancy.Columns.Activated").Filterable().Sortable())
            .Column(t => t.CustomDomain, c => c.Label("Custom Domain").LabelKey("MultiTenancy.Columns.CustomDomain").Filterable())
            .Column(t => t.IsDeleted, c => c.Label("Deleted").LabelKey("MultiTenancy.Columns.IsDeleted").Filterable().Sortable())
            .Column(t => t.CreatedAt, c => c.Label("Created At").LabelKey("MultiTenancy.Columns.CreatedAt").Sortable())
            .Column(t => t.ModifiedAt, c => c.Label("Modified At").LabelKey("MultiTenancy.Columns.ModifiedAt").Sortable())
            .AllowGroupBy(t => t.Activated)
            .GlobalSearch(t => t.Name, t => t.Identifier, t => t.ContactEmail)
            .DateFilter(t => t.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
