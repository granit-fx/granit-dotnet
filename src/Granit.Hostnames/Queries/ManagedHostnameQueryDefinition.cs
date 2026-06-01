using Granit.Hostnames.Domain;
using Granit.QueryEngine;

namespace Granit.Hostnames.Queries;

/// <summary>Query definition for managed hostnames — columns, filters, sorting and search.</summary>
public sealed class ManagedHostnameQueryDefinition : QueryDefinition<ManagedHostname>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Hostnames.ManagedHostnameQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<ManagedHostname> builder)
    {
        builder
            .Column(e => e.Host.Value, c => c.Label("Host").LabelKey("Hostnames.Columns.Host").Filterable().Sortable())
            .Column(e => e.OwnerType, c => c.Label("Owner Type").LabelKey("Hostnames.Columns.OwnerType").Filterable().Sortable())
            .Column(e => e.OwnerId, c => c.Label("Owner Id").LabelKey("Hostnames.Columns.OwnerId").Filterable())
            .Column(e => e.IsPrimary, c => c.Label("Primary").LabelKey("Hostnames.Columns.IsPrimary").Filterable().Sortable())
            .Column(e => e.Status, c => c.Label("Status").LabelKey("Hostnames.Columns.Status").Filterable().Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Hostnames.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("Hostnames.Columns.ModifiedAt").Sortable())
            .GlobalSearch(e => e.Host.Value, e => e.OwnerType)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("host")
            .DefaultPageSize(25);
    }
}
