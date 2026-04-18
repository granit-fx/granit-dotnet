using Granit.DataExchange.Export;
using Granit.Workflow.Domain;

namespace Granit.Workflow.Exports;

public sealed class WorkflowTransitionRecordExportDefinition : ExportDefinition<WorkflowTransitionRecord>
{
    public override string Name => "Granit.Workflow.WorkflowTransitionRecordExport";

    protected override void Configure(ExportDefinitionBuilder<WorkflowTransitionRecord> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.EntityType)
            .Field(e => e.EntityId)
            .Field(e => e.PreviousState)
            .Field(e => e.NewState)
            .Field(e => e.TransitionedAt, f => f.Format("O"))
            .Field(e => e.TransitionedBy)
            .Field(e => e.Comment)
            .Field(e => e.TenantId);
    }
}
