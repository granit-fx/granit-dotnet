using Granit.Domain;

namespace Granit.AI.EntityFrameworkCore.Entities;

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

    public decimal? EstimatedCost { get; set; }

    public string? CostCurrency { get; set; }

    public TimeSpan? Duration { get; set; }

    public Guid? ConversationId { get; set; }

    public string? PromptVersion { get; set; }

    public string? PromptTemplateName { get; set; }

    public int? PromptTemplateVersion { get; set; }

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
        EstimatedCost = record.EstimatedCost,
        CostCurrency = record.CostCurrency,
        Duration = record.Duration,
        ConversationId = record.ConversationId,
        PromptVersion = record.PromptVersion,
        PromptTemplateName = record.PromptTemplateName,
        PromptTemplateVersion = record.PromptTemplateVersion,
    };
}
