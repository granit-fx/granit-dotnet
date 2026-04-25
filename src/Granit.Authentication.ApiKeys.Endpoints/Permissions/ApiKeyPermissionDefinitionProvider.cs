using Granit.Authentication.ApiKeys.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Authentication.ApiKeys.Endpoints.Permissions;

/// <summary>
/// Registers API key management permissions with the authorization system.
/// </summary>
internal sealed class ApiKeyPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            ApiKeyPermissions.GroupName,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "PermissionGroup:AuthenticationApiKeys"));

        group.AddPermission(
            ApiKeyPermissions.Keys.Read,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:AuthenticationApiKeys.Keys.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            ApiKeyPermissions.Keys.Create,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:AuthenticationApiKeys.Keys.Create"),
            MultiTenancySides.Both);

        group.AddPermission(
            ApiKeyPermissions.Keys.Revoke,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:AuthenticationApiKeys.Keys.Revoke"),
            MultiTenancySides.Both);

        group.AddPermission(
            ApiKeyPermissions.Keys.Rotate,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:AuthenticationApiKeys.Keys.Rotate"),
            MultiTenancySides.Both);

        group.AddPermission(
            ApiKeyPermissions.Keys.UpdateScopes,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:AuthenticationApiKeys.Keys.UpdateScopes"),
            MultiTenancySides.Both);
    }
}
