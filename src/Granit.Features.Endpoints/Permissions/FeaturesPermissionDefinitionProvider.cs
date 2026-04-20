using Granit.Authorization;
using Granit.Features.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

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

        // Reading flag values is legitimate in either context (a tenant reads the effective
        // values for its own scope).
        group.AddPermission(
            FeaturesPermissions.Flags.Read,
            LocalizableString.Create<FeaturesEndpointsLocalizationResource>(
                "Permission:Features.Flags.Read"),
            MultiTenancySide.Both);

        // Defining/toggling feature flags is a platform concern — tenant-level overrides,
        // if needed, warrant a dedicated permission rather than reusing this one.
        group.AddPermission(
            FeaturesPermissions.Flags.Manage,
            LocalizableString.Create<FeaturesEndpointsLocalizationResource>(
                "Permission:Features.Flags.Manage"),
            MultiTenancySide.Host);
    }
}
