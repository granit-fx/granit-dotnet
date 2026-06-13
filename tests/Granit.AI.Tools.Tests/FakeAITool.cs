using System.Text.Json;

namespace Granit.AI.Tools.Tests;

/// <summary>
/// Minimal <see cref="IAITool"/> for tests: records the arguments it was invoked with and
/// returns a canned result.
/// </summary>
internal sealed class FakeAITool(
    string name = "fake_tool",
    string description = "A fake tool.",
    string result = "ok") : IAITool
{
    public string Name { get; } = name;

    public string Description { get; } = description;

    public JsonElement ParameterSchema { get; } = AIToolSchema.Empty;

    /// <summary>The raw JSON of the last invocation's arguments, or <c>null</c> if never called.</summary>
    public string? LastArgumentsJson { get; private set; }

    public ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        LastArgumentsJson = context.Arguments.GetRawText();
        return ValueTask.FromResult(AIToolResult.Success(result));
    }
}
