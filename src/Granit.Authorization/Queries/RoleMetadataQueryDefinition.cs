using Granit.Authorization.Domain;
using Granit.QueryEngine;

namespace Granit.Authorization.Queries;

/// <summary>
/// Query definition for <see cref="RoleMetadata"/> — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class RoleMetadataQueryDefinition : QueryDefinition<RoleMetadata>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Authorization.RoleMetadataQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<RoleMetadata> builder)
    {
        builder
            .Column(r => r.Name, c => c.Label("Name").LabelKey("Authorization.Columns.RoleName").Filterable().Sortable())
            .Column(r => r.TenantId, c => c.Label("Tenant").LabelKey("Authorization.Columns.Tenant").Filterable().Sortable())
            .Column(r => r.ClientId, c => c.Label("Client").LabelKey("Authorization.Columns.ClientId").Filterable().Sortable())
            .Column(r => r.MultiTenancySide, c => c.Label("Side").LabelKey("Authorization.Columns.MultiTenancySide").Filterable().Sortable())
            .Column(r => r.Description, c => c.Label("Description").LabelKey("Authorization.Columns.Description").Filterable())
            .Column(r => r.IsSystem, c => c.Label("System").LabelKey("Authorization.Columns.IsSystem").Filterable().Sortable())
            .Column(r => r.CreatedAt, c => c.Label("Created At").LabelKey("Authorization.Columns.CreatedAt").Sortable())
            .Column(r => r.ModifiedAt, c => c.Label("Modified At").LabelKey("Authorization.Columns.ModifiedAt").Sortable())
            .GlobalSearch(r => r.Name, r => r.Description!)
            .DateFilter(r => r.CreatedAt)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
