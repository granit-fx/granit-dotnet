using Granit.Authorization;
using Granit.BlobStorage.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.BlobStorage.Endpoints.Permissions;

/// <summary>
/// Declares the <c>BlobStorage.Administration.*</c> permissions in the Granit RBAC system.
/// </summary>
internal sealed class BlobStoragePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            BlobStoragePermissions.GroupName,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "PermissionGroup:BlobStorage"));

        group.AddPermission(
            BlobStoragePermissions.Administration.Read,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Administration.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            BlobStoragePermissions.Administration.Manage,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Administration.Manage"),
            MultiTenancySides.Both);
    }
}
