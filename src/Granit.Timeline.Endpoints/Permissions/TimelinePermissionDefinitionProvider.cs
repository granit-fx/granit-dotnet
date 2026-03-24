using Granit.Authorization.Abstractions;
using Granit.Localization;
using Granit.Timeline.Endpoints.Internal;

namespace Granit.Timeline.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Timeline.Entries.Read</c> and <c>Timeline.Entries.Create</c> permissions
/// in the Granit RBAC system.
/// </summary>
/// <remarks>
/// Registered automatically by <see cref="GranitTimelineEndpointsModule"/>.
/// Once registered, <c>DynamicPermissionPolicyProvider</c> creates the authorization policies
/// via <c>PermissionRequirement</c> — the full <c>IPermissionChecker</c> pipeline is used.
/// </remarks>
internal sealed class TimelinePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            TimelinePermissions.GroupName,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "PermissionGroup:Timeline"));

        group.AddPermission(
            TimelinePermissions.Entries.Read,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.Entries.Read"));

        group.AddPermission(
            TimelinePermissions.Entries.Create,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.Entries.Create"));
    }
}
