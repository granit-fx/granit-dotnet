namespace Granit.AI;

/// <summary>
/// Ambient, scoped enrichment context for stamped <see cref="AIUsageRecord"/>s.
/// </summary>
/// <remarks>
/// Usage records are stamped automatically by the usage-tracking middleware that
/// <see cref="IAIChatClientFactory"/> and <see cref="IAIEmbeddingGeneratorFactory"/> apply to
/// every client they create. Components that own conversation- or prompt-level context (e.g.
/// the agentic orchestrator) populate this scoped instance before running the model calls, and
/// <see cref="IAIUsageRecordFactory"/> copies the fields onto every record created in the same
/// scope. Requires a scope per logical request — two concurrent runs sharing one DI scope would
/// cross-contaminate each other's enrichment.
/// </remarks>
public sealed class AIUsageContext
{
    /// <summary>Conversation the current interaction belongs to (ADR-067), or <c>null</c>.</summary>
    public Guid? ConversationId { get; set; }

    /// <summary>Version of the guardrail/system prompt in effect, or <c>null</c>.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>Name of the invoked catalogue prompt template, or <c>null</c>.</summary>
    public string? PromptTemplateName { get; set; }

    /// <summary>Revision of the invoked catalogue prompt template, or <c>null</c>.</summary>
    public int? PromptTemplateVersion { get; set; }

    /// <summary>Resets every enrichment field to <c>null</c>.</summary>
    public void Clear()
    {
        ConversationId = null;
        PromptVersion = null;
        PromptTemplateName = null;
        PromptTemplateVersion = null;
    }
}
