using Granit.Mcp.Server.Options;
using Microsoft.Extensions.Options;

namespace Granit.Mcp.Server.Internal;

/// <summary>
/// Hides tools whose declaring type namespace does not match one of the
/// configured <see cref="GranitMcpServerOptions.EnabledModules"/>.
/// </summary>
internal sealed class ModuleScopeVisibilityFilter(IOptions<GranitMcpServerOptions> options) : IMcpToolVisibilityFilter
{
    public ValueTask<bool> IsVisibleAsync(
        string toolName,
        Type? toolType,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        HashSet<string> enabledModules = options.Value.EnabledModules;

        if (enabledModules.Count == 0)
        {
            return ValueTask.FromResult(true);
        }

        if (toolType is null)
        {
            return ValueTask.FromResult(false);
        }

        string? ns = toolType.Namespace;
        if (ns is null)
        {
            return ValueTask.FromResult(false);
        }

        bool isEnabled = enabledModules.Any(module =>
            ns.Contains($".{module}.", StringComparison.OrdinalIgnoreCase) ||
            ns.EndsWith($".{module}", StringComparison.OrdinalIgnoreCase));

        return ValueTask.FromResult(isEnabled);
    }
}
