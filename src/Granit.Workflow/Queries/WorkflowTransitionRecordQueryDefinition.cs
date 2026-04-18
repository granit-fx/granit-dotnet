using Granit.QueryEngine;
using Granit.Workflow.Domain;

namespace Granit.Workflow.Queries;

/// <summary>
/// Query definition for workflow transition records — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class WorkflowTransitionRecordQueryDefinition : QueryDefinition<WorkflowTransitionRecord>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Workflow.WorkflowTransitionRecordQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<WorkflowTransitionRecord> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("Workflow.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.EntityType, c => c.Label("Entity Type").LabelKey("Workflow.Columns.EntityType").Filterable().Sortable())
            .Column(e => e.EntityId, c => c.Label("Entity ID").LabelKey("Workflow.Columns.EntityId").Filterable().Sortable())
            .Column(e => e.PreviousState, c => c.Label("Previous State").LabelKey("Workflow.Columns.PreviousState").Filterable().Sortable())
            .Column(e => e.NewState, c => c.Label("New State").LabelKey("Workflow.Columns.NewState").Filterable().Sortable())
            .Column(e => e.TransitionedAt, c => c.Label("Transitioned At").LabelKey("Workflow.Columns.TransitionedAt").Sortable())
            .Column(e => e.TransitionedBy, c => c.Label("Transitioned By").LabelKey("Workflow.Columns.TransitionedBy").Filterable().Sortable())
            .GlobalSearch(e => e.EntityType, e => e.EntityId, e => e.TransitionedBy)
            .DateFilter(e => e.TransitionedAt)
            .DefaultSort("-transitionedAt")
            .DefaultPageSize(25);
    }
}
