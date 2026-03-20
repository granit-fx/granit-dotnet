using Granit.Authorization.Abstractions;
using Granit.Core.Localization;
using Granit.Features.Endpoints.Internal;

namespace Granit.Features.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for feature management endpoints.
/// </summary>
internal sealed class FeaturesPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            FeaturesPermissions.GroupName,
            LocalizableString.Create<FeaturesEndpointsLocalizationResource>(
                "PermissionGroup:Features"));

        group.AddPermission(
            FeaturesPermissions.Flags.Read,
            LocalizableString.Create<FeaturesEndpointsLocalizationResource>(
                "Permission:Features.Flags.Read"));

        group.AddPermission(
            FeaturesPermissions.Flags.Manage,
            LocalizableString.Create<FeaturesEndpointsLocalizationResource>(
                "Permission:Features.Flags.Manage"));
    }
}
