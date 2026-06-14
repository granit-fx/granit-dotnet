namespace Granit.AI.Chat.Exceptions;

/// <summary>
/// Raised when a message is sent to a workspace whose model does not support chat completions
/// (ADR-067). The request is rejected before the orchestration loop starts.
/// </summary>
public sealed class WorkspaceNotChatCapableException(string workspaceName)
    : InvalidOperationException($"AI workspace '{workspaceName}' is not chat-capable and cannot be used for conversations.")
{
    /// <summary>The offending workspace name.</summary>
    public string WorkspaceName { get; } = workspaceName;
}
