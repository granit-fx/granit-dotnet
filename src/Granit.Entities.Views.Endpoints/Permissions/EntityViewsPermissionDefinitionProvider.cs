using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Entities.Views.Endpoints.Permissions;

/// <summary>
/// Declares the closed permission set for EntityView endpoints (per ADR-047 §6).
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class EntityViewsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            EntityViewPermissions.GroupName,
            LocalizableString.Create<EntityViewsEndpointsLocalizationResource>(
                "PermissionGroup:Entities.Views"));

        group.AddPermission(
            EntityViewPermissions.Read,
            LocalizableString.Create<EntityViewsEndpointsLocalizationResource>(
                "Permission:Entities.Views.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            EntityViewPermissions.Create,
            LocalizableString.Create<EntityViewsEndpointsLocalizationResource>(
                "Permission:Entities.Views.Create"),
            MultiTenancySides.Both);

        group.AddPermission(
            EntityViewPermissions.Share,
            LocalizableString.Create<EntityViewsEndpointsLocalizationResource>(
                "Permission:Entities.Views.Share"),
            MultiTenancySides.Both);

        group.AddPermission(
            EntityViewPermissions.Manage,
            LocalizableString.Create<EntityViewsEndpointsLocalizationResource>(
                "Permission:Entities.Views.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            EntityViewPermissions.DeleteAny,
            LocalizableString.Create<EntityViewsEndpointsLocalizationResource>(
                "Permission:Entities.Views.DeleteAny"),
            MultiTenancySides.Both);
    }
}
