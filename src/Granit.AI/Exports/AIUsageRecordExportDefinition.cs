using Granit.DataExchange.Export;

namespace Granit.AI.Exports;

/// <summary>
/// Export definition for AI usage records — token consumption, cost, and provider context.
/// </summary>
public sealed class AIUsageRecordExportDefinition : ExportDefinition<AIUsageRecord>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.AIUsageRecordExport";

    /// <inheritdoc/>
    public override string? QueryDefinitionName => "Granit.AI.AIUsageRecordQuery";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<AIUsageRecord> builder)
    {
        builder
            .IncludeId()
            .Field(r => r.TenantId)
            .Field(r => r.UserId)
            .Field(r => r.WorkspaceName)
            .Field(r => r.Provider)
            .Field(r => r.Model)
            .Field(r => r.InputTokens)
            .Field(r => r.OutputTokens)
            .Field(r => r.EstimatedCost, f => f.Format("#,##0.0000"))
            .Field(r => r.CostCurrency)
            .Field(r => r.Timestamp, f => f.Format("O"))
            .Field(r => r.Duration);
    }
}
