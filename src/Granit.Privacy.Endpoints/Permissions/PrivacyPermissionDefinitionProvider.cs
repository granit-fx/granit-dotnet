using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Privacy.Endpoints.Internal;

namespace Granit.Privacy.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Privacy.*.*</c> permissions in the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c> — no manual registration needed.
/// </summary>
internal sealed class PrivacyPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            PrivacyPermissions.GroupName,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "PermissionGroup:Privacy"));

        group.AddPermission(
            PrivacyPermissions.Exports.Execute,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.Exports.Execute"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.Exports.ExecuteOnBehalfOf,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.Exports.ExecuteOnBehalfOf"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.Deletions.Execute,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.Deletions.Execute"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.Purposes.Read,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.Purposes.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.Agreements.Read,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.Agreements.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.Agreements.Create,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.Agreements.Create"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.LegalDocuments.Read,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.LegalDocuments.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.LegalDocuments.Create,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.LegalDocuments.Create"),
            MultiTenancySides.Both);

        group.AddPermission(
            PrivacyPermissions.LegalDocuments.Manage,
            LocalizableString.Create<PrivacyEndpointsLocalizationResource>(
                "Permission:Privacy.LegalDocuments.Manage"),
            MultiTenancySides.Both);
    }
}
