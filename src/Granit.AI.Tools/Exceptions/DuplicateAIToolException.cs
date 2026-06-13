namespace Granit.AI.Tools.Exceptions;

/// <summary>
/// Raised when two registered <see cref="IAITool"/> declare the same
/// <see cref="IAITool.Name"/>. Tool names are the model-facing identity and must be
/// unique within a registry.
/// </summary>
public sealed class DuplicateAIToolException(string toolName)
    : InvalidOperationException($"More than one AI tool is registered with the name '{toolName}'. Tool names must be unique.")
{
    /// <summary>The duplicated tool name.</summary>
    public string ToolName { get; } = toolName;
}
