using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Parties.Endpoints.Internal;

namespace Granit.Parties.Endpoints.Permissions;

/// <summary>Declares permission definitions for contacts administration endpoints.</summary>
internal sealed class PartiesPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            PartiesPermissions.GroupName,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "PermissionGroup:Parties"));

        // Visible at both host (managing host-scoped contacts e.g. tenants-as-customers)
        // and tenant (managing the tenant's own end-customers / vendors / leads).
        group.AddPermission(
            PartiesPermissions.Parties.Read,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "Permission:Parties.Parties.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            PartiesPermissions.Parties.Manage,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "Permission:Parties.Parties.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            PartiesPermissions.Parties.Lifecycle,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "Permission:Parties.Parties.Lifecycle"),
            MultiTenancySides.Both);

        group.AddPermission(
            PartiesPermissions.Parties.SetTaxStatus,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "Permission:Parties.Parties.SetTaxStatus"),
            MultiTenancySides.Both);

        group.AddPermission(
            PartiesPermissions.Parties.ExternalMappings,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "Permission:Parties.Parties.ExternalMappings"),
            MultiTenancySides.Both);

        group.AddPermission(
            PartiesPermissions.Parties.Merge,
            LocalizableString.Create<PartiesEndpointsLocalizationResource>(
                "Permission:Parties.Parties.Merge"),
            MultiTenancySides.Both);
    }
}
