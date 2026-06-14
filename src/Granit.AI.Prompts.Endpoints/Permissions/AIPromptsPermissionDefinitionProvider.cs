using Granit.AI.Prompts.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;

namespace Granit.AI.Prompts.Endpoints.Permissions;

/// <summary>
/// Declares the prompt-catalogue permissions. Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class AIPromptsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AIPromptsPermissions.GroupName,
            LocalizableString.Create<AIPromptsEndpointsLocalizationResource>("PermissionGroup:AIPrompts"));

        group.AddPermission(
            AIPromptsPermissions.Templates.Read,
            LocalizableString.Create<AIPromptsEndpointsLocalizationResource>("Permission:AIPrompts.Templates.Read"));

        group.AddPermission(
            AIPromptsPermissions.Templates.Manage,
            LocalizableString.Create<AIPromptsEndpointsLocalizationResource>("Permission:AIPrompts.Templates.Manage"));

        group.AddPermission(
            AIPromptsPermissions.Templates.Delete,
            LocalizableString.Create<AIPromptsEndpointsLocalizationResource>("Permission:AIPrompts.Templates.Delete"));
    }
}
