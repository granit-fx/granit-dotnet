using Granit.Authorization.Domain;
using Granit.QueryEngine;

namespace Granit.Authorization.Queries;

/// <summary>
/// Query definition for permission grants — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class PermissionGrantQueryDefinition : QueryDefinition<PermissionGrant>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Authorization.PermissionGrantQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PermissionGrant> builder)
    {
        builder
            .Column(p => p.TenantId, c => c.Label("Tenant").LabelKey("Authorization.Columns.Tenant").Filterable().Sortable())
            .Column(p => p.Name, c => c.Label("Permission").LabelKey("Authorization.Columns.Permission").Filterable().Sortable())
            .Column(p => p.RoleName, c => c.Label("Role").LabelKey("Authorization.Columns.Role").Filterable().Sortable())
            .Column(p => p.CreatedAt, c => c.Label("Created At").LabelKey("Authorization.Columns.CreatedAt").Sortable())
            .Column(p => p.ModifiedAt, c => c.Label("Modified At").LabelKey("Authorization.Columns.ModifiedAt").Sortable())
            .GlobalSearch(p => p.Name, p => p.RoleName)
            .DateFilter(p => p.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
