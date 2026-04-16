namespace Granit.AI.Exceptions;

/// <summary>
/// Thrown when a requested AI workspace exists but is not active.
/// </summary>
public sealed class AIWorkspaceNotActiveException(string workspaceName)
    : InvalidOperationException($"AI workspace '{workspaceName}' is not active.")
{
    /// <summary>
    /// Name of the inactive workspace.
    /// </summary>
    public string WorkspaceName { get; } = workspaceName;
}
