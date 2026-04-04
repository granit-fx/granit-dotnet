using System.Diagnostics;
using Granit.AI.Workspaces;
using Granit.Guids;
using Microsoft.Extensions.AI;

namespace Granit.AI.Internal;

/// <summary>
/// Default implementation of <see cref="IAIChatCompletionService"/>. Resolves the workspace,
/// invokes the chat client, and records usage.
/// </summary>
internal sealed class DefaultAIChatCompletionService(
    IAIChatClientFactory chatClientFactory,
    IAIWorkspaceProvider workspaceProvider,
    IAIUsageTracker usageTracker,
    TimeProvider timeProvider,
    IGuidGenerator guidGenerator) : IAIChatCompletionService
{
    /// <inheritdoc/>
    public async Task<AIChatCompletionResult?> CompleteAsync(
        string workspaceName,
        IReadOnlyList<AIChatMessage> messages,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await workspaceProvider
            .GetAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        if (workspace is null)
        {
            return null;
        }

        IChatClient chatClient = await chatClientFactory
            .CreateAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        var chatMessages = messages
            .Select(m => new ChatMessage(MapRole(m.Role), m.Content))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        ChatResponse response = await chatClient
            .GetResponseAsync(chatMessages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        string content = response.Text ?? string.Empty;
        UsageDetails? usage = response.Usage;

        int? inputTokens = null;
        int? outputTokens = null;

        if (usage is not null)
        {
            inputTokens = (int)(usage.InputTokenCount ?? 0);
            outputTokens = (int)(usage.OutputTokenCount ?? 0);

            await usageTracker.RecordAsync(new AIUsageRecord
            {
                Id = guidGenerator.Create(),
                WorkspaceName = workspaceName,
                Provider = workspace.Provider,
                Model = workspace.Model,
                InputTokens = inputTokens.Value,
                OutputTokens = outputTokens.Value,
                Timestamp = timeProvider.GetUtcNow(),
                Duration = stopwatch.Elapsed,
            }, cancellationToken).ConfigureAwait(false);
        }

        return new AIChatCompletionResult(
            workspaceName,
            workspace.Model,
            content,
            inputTokens,
            outputTokens,
            stopwatch.Elapsed);
    }

    private static ChatRole MapRole(string role) => role switch
    {
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        _ => new ChatRole(role),
    };
}
