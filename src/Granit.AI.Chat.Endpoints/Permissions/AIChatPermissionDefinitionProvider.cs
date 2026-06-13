using Granit.AI.Chat.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;

namespace Granit.AI.Chat.Endpoints.Permissions;

/// <summary>
/// Declares the chat conversation permissions. Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class AIChatPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AIChatPermissions.GroupName,
            LocalizableString.Create<AIChatEndpointsLocalizationResource>("PermissionGroup:AIChat"));

        group.AddPermission(
            AIChatPermissions.Conversations.Read,
            LocalizableString.Create<AIChatEndpointsLocalizationResource>("Permission:AIChat.Conversations.Read"));

        group.AddPermission(
            AIChatPermissions.Conversations.Manage,
            LocalizableString.Create<AIChatEndpointsLocalizationResource>("Permission:AIChat.Conversations.Manage"));

        group.AddPermission(
            AIChatPermissions.Conversations.Delete,
            LocalizableString.Create<AIChatEndpointsLocalizationResource>("Permission:AIChat.Conversations.Delete"));
    }
}
