using System.Diagnostics.CodeAnalysis;

namespace Granit.AI.Tools;

/// <summary>
/// The set of <see cref="IAITool"/> an application has opted in for the current scope.
/// Built from the tools registered via <c>AddGranitAITools</c>; a tool is absent unless
/// it was explicitly registered.
/// </summary>
public interface IAIToolRegistry
{
    /// <summary>All registered tools, in registration order, names guaranteed unique.</summary>
    IReadOnlyList<IAITool> Tools { get; }

    /// <summary>Looks up a tool by its <see cref="IAITool.Name"/>.</summary>
    /// <param name="name">The tool's wire name.</param>
    /// <param name="tool">The resolved tool, or <see langword="null"/> when not registered.</param>
    /// <returns><see langword="true"/> when a tool with that name is registered.</returns>
    bool TryGet(string name, [NotNullWhen(true)] out IAITool? tool);
}
