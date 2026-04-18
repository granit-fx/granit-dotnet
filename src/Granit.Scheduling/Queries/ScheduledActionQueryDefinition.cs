using Granit.QueryEngine;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Internal;

namespace Granit.Scheduling.Queries;

/// <summary>
/// Query definition for scheduled actions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class ScheduledActionQueryDefinition : QueryDefinition<ScheduledAction>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Scheduling.ScheduledActionsQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(SchedulingLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<ScheduledAction> builder)
    {
        builder
            .Column(a => a.TenantId, c => c.Label("Tenant").LabelKey("Scheduling.Columns.Tenant").Filterable().Sortable())
            .Column(a => a.PayloadType, c => c.Label("Payload Type").LabelKey("Scheduling.Columns.PayloadType").Filterable().Sortable())
            .Column(a => a.Status, c => c.Label("Status").LabelKey("Scheduling.Columns.Status").Filterable().Sortable())
            .Column(a => a.ExecuteAt, c => c.Label("Execute At").LabelKey("Scheduling.Columns.ExecuteAt").Filterable().Sortable())
            .Column(a => a.ExecutedAt, c => c.Label("Executed At").LabelKey("Scheduling.Columns.ExecutedAt").Sortable())
            .Column(a => a.CorrelationId, c => c.Label("Correlation ID").LabelKey("Scheduling.Columns.CorrelationId").Filterable())
            .Column(a => a.CancelledBy, c => c.Label("Cancelled By").LabelKey("Scheduling.Columns.CancelledBy").Filterable())
            .Column(a => a.FailureReason, c => c.Label("Failure Reason").LabelKey("Scheduling.Columns.FailureReason"))
            .GlobalSearch(a => a.PayloadType, a => a.CorrelationId)
            .DateFilter(a => a.ExecuteAt)
            .DefaultSort("-executeAt")
            .DefaultPageSize(25);
    }
}
