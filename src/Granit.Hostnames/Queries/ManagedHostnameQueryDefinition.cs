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
            // Host is a Hostname value object (SingleValueObject<string>). It is mapped by a
            // ValueConverter, so it supports equality/IN filtering and sorting (whole-value
            // round-trips), but NOT substring search: EF Core cannot translate LIKE over a
            // converter. Hence .Filterable()/.Sortable() but no GlobalSearch on Host — selecting
            // `e => e.Host.Value` to dodge that throws at definition build. See issue #2767.
            .Column(e => e.Host, c => c.Label("Host").LabelKey("Hostnames.Columns.Host").Filterable().Sortable())
            .Column(e => e.OwnerType, c => c.Label("Owner Type").LabelKey("Hostnames.Columns.OwnerType").Filterable().Sortable())
            .Column(e => e.OwnerId, c => c.Label("Owner Id").LabelKey("Hostnames.Columns.OwnerId").Filterable().Lookup("users", requiredPermission: "Identity.Users.Read"))
            .Column(e => e.IsPrimary, c => c.Label("Primary").LabelKey("Hostnames.Columns.IsPrimary").Filterable().Sortable())
            .Column(e => e.Status, c => c.Label("Status").LabelKey("Hostnames.Columns.Status").Filterable().Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Hostnames.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("Hostnames.Columns.ModifiedAt").Sortable())
            .AllowGroupBy(e => e.Status)
            .GlobalSearch(e => e.OwnerType)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("host")
            .DefaultPageSize(25);
    }
}
