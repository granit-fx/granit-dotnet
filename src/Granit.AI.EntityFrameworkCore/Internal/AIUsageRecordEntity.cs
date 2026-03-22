using Granit.Core.Domain;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity for AI usage tracking records. Immutable after creation.
/// </summary>
internal sealed class AIUsageRecordEntity : CreationAuditedEntity, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string WorkspaceName { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal? EstimatedCostUsd { get; set; }

    public TimeSpan? Duration { get; set; }

    public static AIUsageRecordEntity FromRecord(AIUsageRecord record) => new()
    {
        Id = record.Id,
        TenantId = record.TenantId,
        UserId = record.UserId,
        WorkspaceName = record.WorkspaceName,
        Provider = record.Provider,
        Model = record.Model,
        InputTokens = record.InputTokens,
        OutputTokens = record.OutputTokens,
        EstimatedCostUsd = record.EstimatedCostUsd,
        Duration = record.Duration,
    };
}
