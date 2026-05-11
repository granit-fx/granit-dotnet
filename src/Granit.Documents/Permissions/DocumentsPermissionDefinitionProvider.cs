using Granit.Authorization;
using Granit.Documents.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Documents.Permissions;

/// <summary>
/// Declares the <c>Documents.*.*</c> permissions in the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c> — no manual registration needed.
/// Covers folders (F2.3), documents (F3), shares (F6), tags (F5 / T6.1), and tenant
/// storage quotas (F7).
/// </summary>
internal sealed class DocumentsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PermissionGroup group = context.AddGroup(
            DocumentsPermissions.GroupName,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "PermissionGroup:Documents"));

        group.AddPermission(
            DocumentsPermissions.Folders.Read,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Folders.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Folders.Manage,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Folders.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Documents.Read,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Documents.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Documents.Manage,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Documents.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Shares.Read,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Shares.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Shares.Manage,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Shares.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Tags.Read,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Tags.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Tags.Manage,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Tags.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPermissions.Quotas.Read,
            LocalizableString.Create<DocumentsLocalizationResource>(
                "Permission:Documents.Quotas.Read"),
            MultiTenancySides.Both);
    }
}
