using Granit.Authorization;
using Granit.Documents.PublicLinks.Localization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Documents.PublicLinks.Permissions;

/// <summary>
/// Declares the <c>DocumentsPublicLinks.PublicLinks.*</c> permissions in the
/// Granit RBAC system. Auto-discovered by <c>GranitAuthorizationModule</c>
/// — no manual registration needed.
/// </summary>
internal sealed class DocumentsPublicLinksPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PermissionGroup group = context.AddGroup(
            DocumentsPublicLinksPermissions.GroupName,
            LocalizableString.Create<DocumentsPublicLinksLocalizationResource>(
                "PermissionGroup:DocumentsPublicLinks"));

        group.AddPermission(
            DocumentsPublicLinksPermissions.PublicLinks.Create,
            LocalizableString.Create<DocumentsPublicLinksLocalizationResource>(
                "Permission:DocumentsPublicLinks.PublicLinks.Create"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPublicLinksPermissions.PublicLinks.Revoke,
            LocalizableString.Create<DocumentsPublicLinksLocalizationResource>(
                "Permission:DocumentsPublicLinks.PublicLinks.Revoke"),
            MultiTenancySides.Both);

        group.AddPermission(
            DocumentsPublicLinksPermissions.PublicLinks.Read,
            LocalizableString.Create<DocumentsPublicLinksLocalizationResource>(
                "Permission:DocumentsPublicLinks.PublicLinks.Read"),
            MultiTenancySides.Both);
    }
}
