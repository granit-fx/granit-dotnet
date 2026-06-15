using Granit.AI;
using Granit.Authorization;

namespace Granit.Localization.AI.Permissions;

/// <summary>
/// Declares the permissions owned by <c>Granit.Localization.AI</c> — the per-tool gate for the
/// <c>translate</c> chat capability (ADR-067) — under the shared <c>AI</c> group.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitAuthorizationModule</c>'s reflection scan over module assemblies.
/// The <c>AI</c> group is also declared by <c>Granit.AI</c>; <see cref="IPermissionDefinitionContext.AddGroup"/>
/// has GetOrAdd semantics, so both providers contribute to the one group and the group's display
/// strings stay in the shared <see cref="AILocalizationResource"/> bundle.
/// </remarks>
internal sealed class LocalizationAIPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            LocalizationAIPermissions.GroupName,
            LocalizableString.Create<AILocalizationResource>("PermissionGroup:AI"));

        // Per-tool gating for the translate chat tool (ADR-067). Off by default — an admin grants
        // the tool's permission to enable it for a user or role.
        group.AddPermission(
            LocalizationAIPermissions.ChatTools.Translate,
            LocalizableString.Create<AILocalizationResource>("Permission:AI.ChatTools.Translate"));
    }
}
