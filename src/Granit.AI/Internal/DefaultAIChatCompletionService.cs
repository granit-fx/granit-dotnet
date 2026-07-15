using System.Diagnostics;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;

namespace Granit.AI.Internal;

/// <summary>
/// Default implementation of <see cref="IAIChatCompletionService"/>. Resolves the workspace and
/// invokes the chat client; usage is stamped by the factory-applied middleware.
/// </summary>
internal sealed class DefaultAIChatCompletionService(
    IAIChatClientFactory chatClientFactory,
    IAIWorkspaceProvider workspaceProvider) : IAIChatCompletionService
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

        // CreateAsync builds a fresh client per call (no cache) — dispose
        // deterministically so the HttpMessageHandler doesn't linger until GC.
        using IChatClient chatClient = await chatClientFactory
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
