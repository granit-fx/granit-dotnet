using Granit.BackgroundJobs.Domain;
using Granit.QueryEngine;

namespace Granit.BackgroundJobs.Queries;

/// <summary>
/// Query definition for background job definitions — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class BackgroundJobDefinitionQueryDefinition : QueryDefinition<BackgroundJobDefinition>
{
    /// <inheritdoc/>
    public override string Name => "Granit.BackgroundJobs.BackgroundJobDefinitionQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<BackgroundJobDefinition> builder)
    {
        builder
            .Column(e => e.JobName, c => c.Label("Job Name").LabelKey("BackgroundJobs.Columns.JobName").Filterable().Sortable())
            .Column(e => e.MessageType, c => c.Label("Message Type").LabelKey("BackgroundJobs.Columns.MessageType").Filterable().Sortable())
            .Column(e => e.CronExpression, c => c.Label("Cron Expression").LabelKey("BackgroundJobs.Columns.CronExpression").Filterable())
            .Column(e => e.IsEnabled, c => c.Label("Enabled").LabelKey("BackgroundJobs.Columns.IsEnabled").Filterable().Sortable())
            .Column(e => e.LastExecutedAt, c => c.Label("Last Executed At").LabelKey("BackgroundJobs.Columns.LastExecutedAt").Sortable())
            .Column(e => e.NextExecutionAt, c => c.Label("Next Execution At").LabelKey("BackgroundJobs.Columns.NextExecutionAt").Sortable())
            .Column(e => e.ConsecutiveFailureCount, c => c.Label("Consecutive Failures").LabelKey("BackgroundJobs.Columns.ConsecutiveFailureCount").Sortable())
            .GlobalSearch(e => e.JobName, e => e.MessageType)
            .DateFilter(e => e.LastExecutedAt)
            .DefaultSort("-lastExecutedAt")
            .DefaultPageSize(25);
    }
}
