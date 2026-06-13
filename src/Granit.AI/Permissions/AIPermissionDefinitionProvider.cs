using Granit.Authorization;
using Granit.Localization;

namespace Granit.AI.Permissions;

/// <summary>
/// Declares the AI module's permissions — currently just <c>AI.Credentials.Manage</c>.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitAuthorizationModule</c>'s reflection scan over module assemblies.
/// </remarks>
internal sealed class AIPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AIPermissions.GroupName,
            LocalizableString.Create<AILocalizationResource>("PermissionGroup:AI"));

        // Tenant + Host: a tenant-admin may rotate their own credentials; a host operator
        // may rotate the Global setting. Both code paths gate on this permission.
        group.AddPermission(
            AIPermissions.Credentials.Manage,
            LocalizableString.Create<AILocalizationResource>("Permission:AI.Credentials.Manage"));

        // Per-tool gating for agentic chat capability tools (ADR-067). Off by default — an admin
        // grants the tool's permission to enable it for a user or role.
        group.AddPermission(
            AIPermissions.ChatTools.Translate,
            LocalizableString.Create<AILocalizationResource>("Permission:AI.ChatTools.Translate"));
    }
}
