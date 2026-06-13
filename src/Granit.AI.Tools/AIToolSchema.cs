using System.Text.Json;

namespace Granit.AI.Tools;

/// <summary>
/// Helpers for the JSON Schema carried by <see cref="IAITool.ParameterSchema"/>.
/// </summary>
public static class AIToolSchema
{
    /// <summary>
    /// The canonical parameter schema for a tool that takes no arguments:
    /// <c>{ "type": "object", "properties": {} }</c>.
    /// </summary>
    public static JsonElement Empty { get; } =
        JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();
}
