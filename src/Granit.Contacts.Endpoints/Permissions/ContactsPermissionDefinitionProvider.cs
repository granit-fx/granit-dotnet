using Granit.Authorization;
using Granit.Contacts.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Contacts.Endpoints.Permissions;

/// <summary>Declares permission definitions for contacts administration endpoints.</summary>
internal sealed class ContactsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            ContactsPermissions.GroupName,
            LocalizableString.Create<ContactsEndpointsLocalizationResource>(
                "PermissionGroup:Contacts"));

        // Visible at both host (managing host-scoped contacts e.g. tenants-as-customers)
        // and tenant (managing the tenant's own end-customers / vendors / leads).
        group.AddPermission(
            ContactsPermissions.Contacts.Read,
            LocalizableString.Create<ContactsEndpointsLocalizationResource>(
                "Permission:Contacts.Contacts.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            ContactsPermissions.Contacts.Manage,
            LocalizableString.Create<ContactsEndpointsLocalizationResource>(
                "Permission:Contacts.Contacts.Manage"),
            MultiTenancySides.Both);
    }
}
