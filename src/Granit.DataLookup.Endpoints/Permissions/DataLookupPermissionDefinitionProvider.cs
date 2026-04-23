using Granit.Authorization;
using Granit.DataLookup.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.DataLookup.Endpoints.Permissions;

/// <summary>
/// Declares <c>DataLookup.Lookups.*</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitAuthorizationModule</c>. The manifest route uses
/// <see cref="DataLookupPermissions.Lookups.Read"/> as its default policy. Individual
/// sources may declare a stricter permission via <c>ILookupSource.RequiredPermission</c>,
/// which is enforced by the dispatch endpoint after the coarse group policy.
/// </remarks>
internal sealed class DataLookupPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            DataLookupPermissions.GroupName,
            LocalizableString.Create<DataLookupEndpointsLocalizationResource>(
                "PermissionGroup:DataLookup"));

        group.AddPermission(
            DataLookupPermissions.Lookups.Read,
            LocalizableString.Create<DataLookupEndpointsLocalizationResource>(
                "Permission:DataLookup.Lookups.Read"),
            MultiTenancySide.Both);
    }
}
