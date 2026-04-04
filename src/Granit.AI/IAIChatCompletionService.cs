using Granit.AI.Workspaces;

namespace Granit.AI;

/// <summary>
/// Result of a chat completion request.
/// </summary>
/// <param name="WorkspaceName">Workspace that processed the request.</param>
/// <param name="Model">Model used for generation.</param>
/// <param name="Content">Generated response content.</param>
/// <param name="InputTokens">Number of input tokens consumed, if available.</param>
/// <param name="OutputTokens">Number of output tokens generated, if available.</param>
/// <param name="Duration">Time taken for the completion.</param>
public sealed record AIChatCompletionResult(
    string WorkspaceName,
    string Model,
    string Content,
    int? InputTokens,
    int? OutputTokens,
    TimeSpan Duration);

/// <summary>
/// A single message in a chat conversation.
/// </summary>
/// <param name="Role">Message role: <c>user</c>, <c>assistant</c>, or <c>system</c>.</param>
/// <param name="Content">Message content.</param>
public sealed record AIChatMessage(string Role, string Content);

/// <summary>
/// Orchestrates AI chat completions: workspace resolution, provider invocation,
/// usage tracking, and error mapping.
/// </summary>
public interface IAIChatCompletionService
{
    /// <summary>
    /// Sends messages to the specified workspace and returns the completion result.
    /// </summary>
    /// <param name="workspaceName">The target workspace name.</param>
    /// <param name="messages">The ordered list of chat messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion result, or <c>null</c> if the workspace does not exist.</returns>
    /// <exception cref="Exceptions.AIWorkspaceNotFoundException">
    /// Thrown when the workspace name does not match any registered provider.
    /// </exception>
    /// <exception cref="Exceptions.AIProviderNotRegisteredException">
    /// Thrown when the provider configured for the workspace is not registered.
    /// </exception>
    Task<AIChatCompletionResult?> CompleteAsync(
        string workspaceName,
        IReadOnlyList<AIChatMessage> messages,
        CancellationToken cancellationToken);
}
