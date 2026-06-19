using Granit.Authorization;
using Granit.Localization.AI.Internal;

namespace Granit.Localization.AI.Permissions;

/// <summary>
/// Declares the permissions owned by <c>Granit.Localization.AI</c> — the per-tool gate for the
/// <c>translate</c> chat capability (ADR-067) — under the shared <c>AI</c> group.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitAuthorizationModule</c>'s reflection scan over module assemblies.
/// The <c>AI</c> group is also declared by <c>Granit.AI</c>; <see cref="IPermissionDefinitionContext.AddGroup"/>
/// has GetOrAdd semantics, so the group's display string is already set by <c>Granit.AI</c>'s
/// own provider and the <c>null</c> display here is intentionally ignored.
/// </remarks>
internal sealed class LocalizationAIPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(LocalizationAIPermissions.GroupName);

        // Per-tool gating for the translate chat tool (ADR-067). Off by default — an admin grants
        // the tool's permission to enable it for a user or role.
        group.AddPermission(
            LocalizationAIPermissions.ChatTools.Translate,
            LocalizableString.Create<LocalizationAILocalizationResource>("Permission:AI.ChatTools.Translate"));
    }
}
