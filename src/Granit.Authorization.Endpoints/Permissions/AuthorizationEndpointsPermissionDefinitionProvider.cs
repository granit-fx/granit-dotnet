using Granit.Authorization;
using Granit.Authorization.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Authorization.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions used to protect the authorization management endpoints.
/// </summary>
internal sealed class AuthorizationEndpointsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AuthorizationEndpointsPermissions.GroupName,
            LocalizableString.Create<AuthorizationEndpointsLocalizationResource>(
                "PermissionGroup:Authorization"));

        group.AddPermission(
            AuthorizationEndpointsPermissions.Definitions.Read,
            LocalizableString.Create<AuthorizationEndpointsLocalizationResource>(
                "Permission:Authorization.Definitions.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            AuthorizationEndpointsPermissions.Grants.Manage,
            LocalizableString.Create<AuthorizationEndpointsLocalizationResource>(
                "Permission:Authorization.Grants.Manage"),
            MultiTenancySides.Both);
    }
}
