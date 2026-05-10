using Granit.Authorization;
using Granit.Documents.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Documents.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Documents.*.*</c> permissions in the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c> — no manual registration needed.
/// </summary>
/// <remarks>
/// Phase 1 ships only the folder permissions (F2.3). Document, share, tag, and quota
/// permissions arrive with their respective stories (F3, F6, F5, F7).
/// </remarks>
internal sealed class DocumentsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PermissionGroup group = context.AddGroup(
            DocumentsPermissions.GroupName,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "PermissionGroup:Documents"));

        group.AddPermission(
            DocumentsPermissions.Folders.Read,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "Permission:Documents.Folders.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Folders.Manage,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "Permission:Documents.Folders.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Documents.Read,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "Permission:Documents.Documents.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Documents.Manage,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "Permission:Documents.Documents.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Shares.Read,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "Permission:Documents.Shares.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Shares.Manage,
            LocalizableString.Create<DocumentsEndpointsLocalizationResource>(
                "Permission:Documents.Shares.Manage"),
            MultiTenancySides.Both);
    }
}
