using Granit.Metering.Domain;
using Granit.QueryEngine;

namespace Granit.Metering.Queries;

/// <summary>
/// Query definition for meter definitions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class MeterDefinitionQueryDefinition : QueryDefinition<MeterDefinition>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Metering.MeterDefinitionQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<MeterDefinition> builder)
    {
        builder
            .Column(m => m.TenantId, c => c
                .Label("Tenant")
                .LabelKey("Metering.Columns.Tenant")
                .Filterable()
                .Sortable()
                .Lookup("tenants", requiredPermission: "MultiTenancy.Tenants.Read"))
            .Column(m => m.Name, c => c.Label("Name").LabelKey("Metering.Columns.Name").Filterable().Sortable())
            .Column(m => m.Unit, c => c.Label("Unit").LabelKey("Metering.Columns.Unit").Filterable().Sortable())
            .Column(m => m.AggregationType, c => c.Label("Aggregation").LabelKey("Metering.Columns.AggregationType").Filterable().Sortable())
            .Column(m => m.LifecycleStatus, c => c.Label("Status").LabelKey("Metering.Columns.LifecycleStatus").Filterable().Sortable())
            .Column(m => m.CreatedAt, c => c.Label("Created At").LabelKey("Metering.Columns.CreatedAt").Sortable())
            .Column(m => m.ModifiedAt, c => c.Label("Modified At").LabelKey("Metering.Columns.ModifiedAt").Sortable())
            .GlobalSearch(m => m.Name, m => m.Unit)
            .DateFilter(m => m.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
