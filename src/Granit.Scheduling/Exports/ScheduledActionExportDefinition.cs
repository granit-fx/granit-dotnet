using Granit.DataExchange.Export;
using Granit.Scheduling.Domain;

namespace Granit.Scheduling.Exports;

public sealed class ScheduledActionExportDefinition : ExportDefinition<ScheduledAction>
{
    public override string Name => "Granit.Scheduling.ScheduledActionExport";

    protected override void Configure(ExportDefinitionBuilder<ScheduledAction> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.PayloadType)
            .Field(e => e.PayloadJson)
            .Field(e => e.ExecuteAt, f => f.Format("O"))
            .Field(e => e.CorrelationId)
            .Field(e => e.Status)
            .Field(e => e.ExecutedAt, f => f.Format("O"))
            .Field(e => e.CancelledBy)
            .Field(e => e.FailureReason)
            .Field(e => e.TenantId)
            .IncludeAuditFields();
    }
}
