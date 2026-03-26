using Granit.Mcp.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Mcp.Server.Internal;

/// <summary>
/// Hides tools annotated with <see cref="McpTenantScopeAttribute"/> when
/// no tenant context is active.
/// </summary>
internal sealed class TenantAwareVisibilityFilter(IOptions<GranitMcpOptions> options) : IMcpToolVisibilityFilter
{
    public ValueTask<bool> IsVisibleAsync(
        string toolName,
        Type? toolType,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.EnableTenantFiltering || toolType is null)
        {
            return ValueTask.FromResult(true);
        }

        McpTenantScopeAttribute? scope = toolType
            .GetCustomAttributes(typeof(McpTenantScopeAttribute), inherit: false)
            .OfType<McpTenantScopeAttribute>()
            .FirstOrDefault();

        if (scope is not { RequireTenant: true })
        {
            return ValueTask.FromResult(true);
        }

        ICurrentTenant? currentTenant = services.GetService<ICurrentTenant>();
        bool isAvailable = currentTenant?.IsAvailable ?? false;
        return ValueTask.FromResult(isAvailable);
    }
}
