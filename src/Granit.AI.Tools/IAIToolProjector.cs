using Microsoft.Extensions.AI;

namespace Granit.AI.Tools;

/// <summary>
/// Projects <see cref="IAITool"/> instances to <see cref="AITool"/> declarations for
/// assignment to <c>ChatOptions.Tools</c>. This is what makes tool declarations emit
/// automatically — the orchestrator never hand-writes a tool list.
/// </summary>
public interface IAIToolProjector
{
    /// <summary>Projects an explicit set of tools to <see cref="AITool"/> declarations.</summary>
    IReadOnlyList<AITool> Project(IEnumerable<IAITool> tools);

    /// <summary>Projects every tool in the <see cref="IAIToolRegistry"/>.</summary>
    IReadOnlyList<AITool> ProjectAll();
}
