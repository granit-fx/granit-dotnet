using Granit.Authorization;
using Granit.Hostnames.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Hostnames.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for custom hostname management endpoints.
/// Auto-discovered by the Granit authorization module.
/// </summary>
internal sealed class HostnamesPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            HostnamesPermissions.GroupName,
            LocalizableString.Create<HostnamesEndpointsLocalizationResource>(
                "PermissionGroup:Hostnames"));

        // Hostname management is a host-level platform concern — the platform operator
        // registers and owns hostname mappings; individual tenants cannot self-register.
        group.AddPermission(
            HostnamesPermissions.Hostnames.Read,
            LocalizableString.Create<HostnamesEndpointsLocalizationResource>(
                "Permission:Hostnames.Hostnames.Read"),
            MultiTenancySides.Host);

        group.AddPermission(
            HostnamesPermissions.Hostnames.Manage,
            LocalizableString.Create<HostnamesEndpointsLocalizationResource>(
                "Permission:Hostnames.Hostnames.Manage"),
            MultiTenancySides.Host);
    }
}
