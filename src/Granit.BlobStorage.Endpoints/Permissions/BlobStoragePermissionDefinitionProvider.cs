using Granit.Authorization.Abstractions;
using Granit.BlobStorage.Endpoints.Internal;
using Granit.Core.Localization;

namespace Granit.BlobStorage.Endpoints.Permissions;

/// <summary>
/// Declares the <c>BlobStorage.Blobs.*</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <see cref="GranitBlobStorageEndpointsModule"/>.
/// Once registered, <c>DynamicPermissionPolicyProvider</c> creates the authorization policy
/// via <c>PermissionRequirement</c> — the full <c>IPermissionChecker</c> pipeline is used:
/// </para>
/// <list type="number">
/// <item><c>AlwaysAllow</c> (dev/test, <c>GranitAuthorizationOptions.AlwaysAllow = true</c>)</item>
/// <item>AdminRole bypass (<c>GranitAuthorizationOptions.AdminRoles</c>)</item>
/// <item>Cache + <c>IPermissionGrantStore</c> query per role</item>
/// </list>
/// <para>
/// In production, grant the permission to the desired Keycloak role via one of:
/// <list type="bullet">
/// <item>Add the role to <c>GranitAuthorizationOptions.AdminRoles</c> in <c>appsettings.json</c></item>
/// <item>Call <c>IPermissionManagerWriter.SetAsync("BlobStorage.Blobs.View", "my-role", tenantId, true)</c></item>
/// </list>
/// </para>
/// </remarks>
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
            BlobStoragePermissions.Blobs.Read,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Blobs.Read"));

        group.AddPermission(
            BlobStoragePermissions.Blobs.Upload,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Blobs.Upload"));

        group.AddPermission(
            BlobStoragePermissions.Blobs.Download,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Blobs.Download"));

        group.AddPermission(
            BlobStoragePermissions.Blobs.Delete,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Blobs.Delete"));

        group.AddPermission(
            BlobStoragePermissions.Blobs.Manage,
            LocalizableString.Create<BlobStorageEndpointsLocalizationResource>(
                "Permission:BlobStorage.Blobs.Manage"));
    }
}
