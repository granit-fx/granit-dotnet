using Granit.Authorization;
using Granit.Localization;
using Granit.ReferenceData.Endpoints.Internal;

namespace Granit.ReferenceData.Endpoints.Permissions;

/// <summary>
/// Declares the <c>ReferenceData.Entries.*</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// Auto-discovered by <see cref="Granit.Authorization.GranitAuthorizationModule"/>.
/// The <c>DynamicPermissionPolicyProvider</c> creates authorization policies from these
/// permission definitions — no manual <c>AddPolicy()</c> calls needed.
/// </remarks>
internal sealed class ReferenceDataPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            ReferenceDataPermissions.GroupName,
            LocalizableString.Create<ReferenceDataEndpointsLocalizationResource>(
                "PermissionGroup:ReferenceData"));

        group.AddPermission(
            ReferenceDataPermissions.Entries.Read,
            LocalizableString.Create<ReferenceDataEndpointsLocalizationResource>(
                "Permission:ReferenceData.Entries.Read"));

        group.AddPermission(
            ReferenceDataPermissions.Entries.Create,
            LocalizableString.Create<ReferenceDataEndpointsLocalizationResource>(
                "Permission:ReferenceData.Entries.Create"));

        group.AddPermission(
            ReferenceDataPermissions.Entries.Manage,
            LocalizableString.Create<ReferenceDataEndpointsLocalizationResource>(
                "Permission:ReferenceData.Entries.Manage"));
    }
}
