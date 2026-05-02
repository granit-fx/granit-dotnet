using Granit.Authorization;
using Granit.Entities.Customization.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Entities.Customization.Endpoints.Permissions;

/// <summary>
/// Declares the <c>EntitiesCustomization.Customizations.*</c> permissions in the
/// Granit RBAC system. Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class EntitiesCustomizationPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            EntitiesCustomizationPermissions.GroupName,
            LocalizableString.Create<EntitiesCustomizationEndpointsLocalizationResource>(
                "PermissionGroup:EntitiesCustomization"));

        group.AddPermission(
            EntitiesCustomizationPermissions.Customizations.Read,
            LocalizableString.Create<EntitiesCustomizationEndpointsLocalizationResource>(
                "Permission:EntitiesCustomization.Customizations.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            EntitiesCustomizationPermissions.Customizations.Manage,
            LocalizableString.Create<EntitiesCustomizationEndpointsLocalizationResource>(
                "Permission:EntitiesCustomization.Customizations.Manage"),
            MultiTenancySides.Both);
    }
}
