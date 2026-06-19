using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.AI.Chat.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for Granit.AI.Chat.
/// Meter: <c>Granit.AI.Chat</c>.
/// Metric names: <c>granit.ai.chat.{entity}.{action}</c>.
/// Tags: <c>tenant_id</c> (coalesced to <c>"global"</c>), <c>workspace_key</c>.
/// </summary>
public sealed class AIChatMetrics
{
    public const string MeterName = "Granit.AI.Chat";

    private readonly Counter<long> _conversationsCreated;
    private readonly Counter<long> _turnsCompleted;
    private readonly Counter<long> _tokensInput;
    private readonly Counter<long> _tokensOutput;

    public AIChatMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _conversationsCreated = meter.CreateCounter<long>(
            "granit.ai.chat.conversation.created",
            description: "Number of new chat conversations created.");

        _turnsCompleted = meter.CreateCounter<long>(
            "granit.ai.chat.turn.completed",
            description: "Number of agentic chat turns completed successfully.");

        _tokensInput = meter.CreateCounter<long>(
            "granit.ai.chat.tokens.input",
            description: "Number of input tokens consumed per chat turn.");

        _tokensOutput = meter.CreateCounter<long>(
            "granit.ai.chat.tokens.output",
            description: "Number of output tokens produced per chat turn.");
    }

    /// <summary>Records a new conversation being created.</summary>
    public void RecordConversationCreated(string? tenantId, string workspaceKey) =>
        _conversationsCreated.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "workspace_key", workspaceKey },
        });

    /// <summary>Records a completed turn including its token usage.</summary>
    public void RecordTurnCompleted(string? tenantId, string workspaceKey, int inputTokens, int outputTokens)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "workspace_key", workspaceKey },
        };

        _turnsCompleted.Add(1, tags);
        _tokensInput.Add(inputTokens, tags);
        _tokensOutput.Add(outputTokens, tags);
    }
}
