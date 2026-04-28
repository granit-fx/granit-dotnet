using Granit.Analytics.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Analytics.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Analytics.*</c> permissions in the Granit RBAC system.
/// </summary>
internal sealed class AnalyticsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AnalyticsPermissions.GroupName,
            LocalizableString.Create<AnalyticsEndpointsLocalizationResource>(
                "PermissionGroup:Analytics"));

        group.AddPermission(
            AnalyticsPermissions.Metrics.Read,
            LocalizableString.Create<AnalyticsEndpointsLocalizationResource>(
                "Permission:Analytics.Metrics.Read"),
            MultiTenancySides.Both);
    }
}
