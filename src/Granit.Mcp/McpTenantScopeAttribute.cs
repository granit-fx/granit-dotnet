namespace Granit.Mcp;

/// <summary>
/// Controls tenant-aware visibility of MCP tool classes.
/// Used by <see cref="IMcpToolVisibilityFilter"/> implementations to hide tools
/// based on the current tenant context.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class McpTenantScopeAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, the tool is only visible when a tenant context
    /// is active (<c>ICurrentTenant.IsAvailable == true</c>).
    /// </summary>
    public bool RequireTenant { get; init; }
}
