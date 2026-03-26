using Granit.Mcp.Options;
using Microsoft.Extensions.Options;

namespace Granit.Mcp.Server.Internal;

/// <summary>
/// Hides tools that lack the <see cref="McpExposedAttribute"/> when
/// <see cref="McpToolDiscoveryMode.Explicit"/> is configured.
/// </summary>
internal sealed class ExplicitDiscoveryFilter(IOptions<GranitMcpOptions> options) : IMcpToolVisibilityFilter
{
    public ValueTask<bool> IsVisibleAsync(
        string toolName,
        Type? toolType,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (options.Value.ToolDiscovery != McpToolDiscoveryMode.Explicit)
        {
            return ValueTask.FromResult(true);
        }

        if (toolType is null)
        {
            return ValueTask.FromResult(true);
        }

        bool hasAttribute = toolType.IsDefined(typeof(McpExposedAttribute), inherit: false);
        return ValueTask.FromResult(hasAttribute);
    }
}
