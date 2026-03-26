using Granit.Authorization.Abstractions;
using Granit.Localization;

namespace Granit.Mcp.Server.Permissions;

/// <summary>
/// Registers MCP permission definitions in the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class McpPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            McpPermissions.GroupName,
            LocalizableString.Create<McpServerLocalizationResource>("PermissionGroup:Mcp"));

        group.AddPermission(
            McpPermissions.Server.Access,
            LocalizableString.Create<McpServerLocalizationResource>("Permission:Mcp.Server.Access"));

        group.AddPermission(
            McpPermissions.Tools.Read,
            LocalizableString.Create<McpServerLocalizationResource>("Permission:Mcp.Tools.Read"));

        group.AddPermission(
            McpPermissions.Tools.Execute,
            LocalizableString.Create<McpServerLocalizationResource>("Permission:Mcp.Tools.Execute"));

        group.AddPermission(
            McpPermissions.Resources.Read,
            LocalizableString.Create<McpServerLocalizationResource>("Permission:Mcp.Resources.Read"));

        group.AddPermission(
            McpPermissions.Prompts.Read,
            LocalizableString.Create<McpServerLocalizationResource>("Permission:Mcp.Prompts.Read"));
    }
}
