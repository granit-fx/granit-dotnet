using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Granit.AI.Tools.Exceptions;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Default <see cref="IAIToolRegistry"/>. Aggregates every <see cref="IAITool"/> registered
/// via <c>AddGranitAITools</c>, validating name shape and uniqueness on construction so a
/// misconfiguration fails fast at the first scope resolution rather than mid-conversation.
/// </summary>
internal sealed partial class AIToolRegistry : IAIToolRegistry
{
    private readonly Dictionary<string, IAITool> _byName;

    public AIToolRegistry(IEnumerable<IAITool> tools)
    {
        Tools = [.. tools];
        _byName = new Dictionary<string, IAITool>(Tools.Count, StringComparer.Ordinal);

        foreach (IAITool tool in Tools)
        {
            if (string.IsNullOrEmpty(tool.Name) || !ToolNamePattern().IsMatch(tool.Name))
            {
                throw new InvalidAIToolNameException(tool.Name ?? string.Empty);
            }

            if (!_byName.TryAdd(tool.Name, tool))
            {
                throw new DuplicateAIToolException(tool.Name);
            }
        }
    }

    public IReadOnlyList<IAITool> Tools { get; }

    public bool TryGet(string name, [NotNullWhen(true)] out IAITool? tool) =>
        _byName.TryGetValue(name, out tool);

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex ToolNamePattern();
}
