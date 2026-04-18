using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;

namespace Granit.AI.Queries;

/// <summary>
/// Query definition for AI usage records — declares columns, filters, sorting,
/// grouping, and aggregates for the query engine.
/// </summary>
public sealed class AIUsageRecordQueryDefinition : QueryDefinition<AIUsageRecord>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.AIUsageRecordQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(AILocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<AIUsageRecord> builder)
    {
        builder
            .Column(r => r.WorkspaceName, c => c.Label("Workspace").LabelKey("AI.Columns.Workspace").Filterable().Sortable())
            .Column(r => r.Provider, c => c.Label("Provider").LabelKey("AI.Columns.Provider").Filterable().Sortable())
            .Column(r => r.Model, c => c.Label("Model").LabelKey("AI.Columns.Model").Filterable().Sortable())
            .Column(r => r.InputTokens, c => c.Label("Input Tokens").LabelKey("AI.Columns.InputTokens").Sortable())
            .Column(r => r.OutputTokens, c => c.Label("Output Tokens").LabelKey("AI.Columns.OutputTokens").Sortable())
            .Column(r => r.EstimatedCost, c => c.Label("Estimated Cost").LabelKey("AI.Columns.EstimatedCost").Sortable())
            .Column(r => r.CostCurrency, c => c.Label("Currency").LabelKey("AI.Columns.CostCurrency"))
            .Column(r => r.Timestamp, c => c.Label("Timestamp").LabelKey("AI.Columns.Timestamp").Sortable())
            .Column(r => r.Duration, c => c.Label("Duration").LabelKey("AI.Columns.Duration"))
            .GlobalSearch(r => r.WorkspaceName, r => r.Provider, r => r.Model)
            .DateFilter(r => r.Timestamp)
            .AllowGroupBy(r => r.WorkspaceName)
            .AllowGroupBy(r => r.Provider)
            .AllowGroupBy(r => r.Model)
            .Aggregate(r => r.InputTokens, AggregateFunction.Sum, "totalInputTokens")
            .Aggregate(r => r.OutputTokens, AggregateFunction.Sum, "totalOutputTokens")
            .Aggregate(r => r.EstimatedCost, AggregateFunction.Sum, "totalEstimatedCost")
            .DefaultSort("-timestamp")
            .DefaultPageSize(25);
    }
}
