using System;
using Granit.Authorization;
using Granit.Browsing.Localization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Browsing.Permissions;

/// <summary>
/// Declares the <c>Granit.Browsing.Pages.*</c> permissions in the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c> — no manual registration needed.
/// </summary>
/// <remarks>
/// Lives in the base <c>Granit.Browsing</c> assembly rather than an <c>.Endpoints</c>
/// package because <c>Granit.Browsing</c> has no HTTP surface — it is a framework
/// primitive consumed by providers and other modules.
/// </remarks>
internal sealed class BrowsingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PermissionGroup group = context.AddGroup(
            BrowsingPermissions.GroupName,
            LocalizableString.Create<BrowsingLocalizationResource>(
                "PermissionGroup:Granit.Browsing"));

        group.AddPermission(
            BrowsingPermissions.Pages.Acquire,
            LocalizableString.Create<BrowsingLocalizationResource>(
                "Permission:Granit.Browsing.Pages.Acquire"),
            MultiTenancySides.Both);

        group.AddPermission(
            BrowsingPermissions.Pages.Navigate,
            LocalizableString.Create<BrowsingLocalizationResource>(
                "Permission:Granit.Browsing.Pages.Navigate"),
            MultiTenancySides.Both);

        group.AddPermission(
            BrowsingPermissions.Pages.InjectScript,
            LocalizableString.Create<BrowsingLocalizationResource>(
                "Permission:Granit.Browsing.Pages.InjectScript"),
            MultiTenancySides.Both);

        group.AddPermission(
            BrowsingPermissions.Pages.BypassCsp,
            LocalizableString.Create<BrowsingLocalizationResource>(
                "Permission:Granit.Browsing.Pages.BypassCsp"),
            MultiTenancySides.Both);

        group.AddPermission(
            BrowsingPermissions.Pages.UseFileScheme,
            LocalizableString.Create<BrowsingLocalizationResource>(
                "Permission:Granit.Browsing.Pages.UseFileScheme"),
            MultiTenancySides.Both);
    }
}
