using Microsoft.Extensions.AI;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Default <see cref="IAIToolProjector"/>: wraps each <see cref="IAITool"/> in a
/// <see cref="GranitAIToolFunction"/>.
/// </summary>
internal sealed class AIToolProjector(IAIToolRegistry registry) : IAIToolProjector
{
    public IReadOnlyList<AITool> Project(IEnumerable<IAITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return [.. tools.Select(static tool => new GranitAIToolFunction(tool))];
    }

    public IReadOnlyList<AITool> ProjectAll() => Project(registry.Tools);
}
