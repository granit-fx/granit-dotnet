namespace Granit.AI.Tools.Exceptions;

/// <summary>
/// Raised when a registered <see cref="IAITool"/> has a <see cref="IAITool.Name"/> that
/// is not a valid provider function name (letters, digits, underscore and hyphen only,
/// non-empty). An invalid name would be rejected — or silently mangled — by the provider.
/// </summary>
public sealed class InvalidAIToolNameException(string toolName)
    : InvalidOperationException($"AI tool name '{toolName}' is invalid. Names must be non-empty and contain only letters, digits, '_' or '-'.")
{
    /// <summary>The offending tool name.</summary>
    public string ToolName { get; } = toolName;
}
