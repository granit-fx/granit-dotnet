using Granit.Querying;
using Granit.Querying.Filtering;

namespace Granit.AI.Endpoints.Queries;

/// <summary>
/// Query definition for AI usage records — declares columns, filters, sorting,
/// grouping, and aggregates for the query engine.
/// </summary>
public sealed class AIUsageRecordQueryDefinition : QueryDefinition<AIUsageRecord>
{
    /// <inheritdoc/>
    public override string Name => "AI.UsageRecords";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<AIUsageRecord> builder)
    {
        builder
            .Column(r => r.WorkspaceName, c => c.Label("Workspace").Filterable().Sortable())
            .Column(r => r.Provider, c => c.Label("Provider").Filterable().Sortable())
            .Column(r => r.Model, c => c.Label("Model").Filterable().Sortable())
            .Column(r => r.InputTokens, c => c.Label("Input Tokens").Sortable())
            .Column(r => r.OutputTokens, c => c.Label("Output Tokens").Sortable())
            .Column(r => r.EstimatedCostUsd, c => c.Label("Estimated Cost (USD)").Sortable())
            .Column(r => r.Timestamp, c => c.Label("Timestamp").Sortable())
            .Column(r => r.Duration, c => c.Label("Duration"))
            .GlobalSearch(r => r.WorkspaceName, r => r.Provider, r => r.Model)
            .DateFilter(r => r.Timestamp)
            .AllowGroupBy(r => r.WorkspaceName)
            .AllowGroupBy(r => r.Provider)
            .AllowGroupBy(r => r.Model)
            .Aggregate(r => r.InputTokens, AggregateFunction.Sum, "totalInputTokens")
            .Aggregate(r => r.OutputTokens, AggregateFunction.Sum, "totalOutputTokens")
            .Aggregate(r => r.EstimatedCostUsd, AggregateFunction.Sum, "totalEstimatedCostUsd")
            .DefaultSort("-timestamp")
            .DefaultPageSize(25);
    }
}
