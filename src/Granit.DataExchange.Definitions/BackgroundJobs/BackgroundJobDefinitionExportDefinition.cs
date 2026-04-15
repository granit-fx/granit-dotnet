using Granit.BackgroundJobs.Domain;
using Granit.DataExchange.Export;

namespace Granit.DataExchange.Definitions.BackgroundJobs;

public sealed class BackgroundJobDefinitionExportDefinition : ExportDefinition<BackgroundJobDefinition>
{
    public override string Name => "Granit.BackgroundJobs.BackgroundJobDefinitionExport";

    protected override void Configure(ExportDefinitionBuilder<BackgroundJobDefinition> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.JobName)
            .Field(e => e.MessageType)
            .Field(e => e.CronExpression)
            .Field(e => e.IsEnabled)
            .Field(e => e.LastExecutedAt, f => f.Format("O"))
            .Field(e => e.NextExecutionAt, f => f.Format("O"))
            .Field(e => e.ConsecutiveFailureCount)
            .Field(e => e.LastErrorMessage)
            .Field(e => e.TriggeredBy);
    }
}
