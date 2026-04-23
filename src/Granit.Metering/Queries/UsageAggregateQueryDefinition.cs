using Granit.Metering.Domain;
using Granit.QueryEngine;

namespace Granit.Metering.Queries;

/// <summary>
/// Query definition for usage aggregates — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class UsageAggregateQueryDefinition : QueryDefinition<UsageAggregate>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Metering.UsageAggregateQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<UsageAggregate> builder)
    {
        builder
            .Column(u => u.TenantId, c => c
                .Label("Tenant")
                .LabelKey("Metering.Columns.Tenant")
                .Filterable()
                .Sortable()
                .Lookup("tenants", requiredPermission: "Platform.Tenants.Read"))
            .Column(u => u.MeterDefinitionId, c => c
                .Label("Meter")
                .LabelKey("Metering.Columns.MeterDefinition")
                .Filterable()
                .Sortable()
                .Lookup("meter-definitions", scopeKeys: ["tenantId"]))
            .Column(u => u.Period, c => c.Label("Period").LabelKey("Metering.Columns.Period").Filterable().Sortable())
            .Column(u => u.PeriodStart, c => c.Label("Period Start").LabelKey("Metering.Columns.PeriodStart").Sortable())
            .Column(u => u.PeriodEnd, c => c.Label("Period End").LabelKey("Metering.Columns.PeriodEnd").Sortable())
            .Column(u => u.AggregatedValue, c => c.Label("Aggregated Value").LabelKey("Metering.Columns.AggregatedValue").Sortable())
            .Column(u => u.EventCount, c => c.Label("Event Count").LabelKey("Metering.Columns.EventCount").Sortable())
            .DateFilter(u => u.PeriodStart)
            .DefaultSort("-periodStart")
            .DefaultPageSize(25);
    }
}
