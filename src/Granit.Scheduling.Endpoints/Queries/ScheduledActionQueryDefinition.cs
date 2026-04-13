using Granit.QueryEngine;
using Granit.Scheduling.Domain;

namespace Granit.Scheduling.Endpoints.Queries;

/// <summary>
/// Query definition for scheduled actions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class ScheduledActionQueryDefinition : QueryDefinition<ScheduledAction>
{
    /// <inheritdoc/>
    public override string Name => "Scheduling.ScheduledActions";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<ScheduledAction> builder)
    {
        builder
            .Column(a => a.TenantId, c => c.Label("Tenant").Filterable().Sortable())
            .Column(a => a.PayloadType, c => c.Label("Payload Type").Filterable().Sortable())
            .Column(a => a.Status, c => c.Label("Status").Filterable().Sortable())
            .Column(a => a.ExecuteAt, c => c.Label("Execute At").Filterable().Sortable())
            .Column(a => a.ExecutedAt, c => c.Label("Executed At").Sortable())
            .Column(a => a.CorrelationId, c => c.Label("Correlation ID").Filterable())
            .Column(a => a.CancelledBy, c => c.Label("Cancelled By").Filterable())
            .Column(a => a.FailureReason, c => c.Label("Failure Reason"))
            .GlobalSearch(a => a.PayloadType, a => a.CorrelationId)
            .DateFilter(a => a.ExecuteAt)
            .DefaultSort("-executeAt")
            .DefaultPageSize(25);
    }
}
