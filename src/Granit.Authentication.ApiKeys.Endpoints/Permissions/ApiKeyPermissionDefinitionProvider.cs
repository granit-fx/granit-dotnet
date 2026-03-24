using Granit.Authentication.ApiKeys.Endpoints.Internal;
using Granit.Authorization.Abstractions;
using Granit.Localization;

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
                "PermissionGroup:ApiKeys"));

        group.AddPermission(
            ApiKeyPermissions.Keys.Read,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:ApiKeys.Keys.Read"));

        group.AddPermission(
            ApiKeyPermissions.Keys.Create,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:ApiKeys.Keys.Create"));

        group.AddPermission(
            ApiKeyPermissions.Keys.Revoke,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:ApiKeys.Keys.Revoke"));

        group.AddPermission(
            ApiKeyPermissions.Keys.Rotate,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:ApiKeys.Keys.Rotate"));

        group.AddPermission(
            ApiKeyPermissions.Keys.UpdateScopes,
            LocalizableString.Create<ApiKeysEndpointsLocalizationResource>(
                "Permission:ApiKeys.Keys.UpdateScopes"));
    }
}
