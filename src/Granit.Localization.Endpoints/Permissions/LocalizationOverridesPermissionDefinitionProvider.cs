using Granit.Authorization;
using Granit.Localization;
using Granit.Localization.Endpoints.Internal;

namespace Granit.Localization.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Localization.Overrides.Read</c> and <c>Localization.Overrides.Manage</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <see cref="GranitLocalizationEndpointsModule"/>.
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
/// <item>Call <c>IPermissionManagerWriter.SetAsync("Localization.Overrides.Manage", "my-role", tenantId, true)</c></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class LocalizationOverridesPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            LocalizationOverridesPermissions.GroupName,
            LocalizableString.Create<LocalizationEndpointsLocalizationResource>(
                "PermissionGroup:Localization"));

        group.AddPermission(
            LocalizationOverridesPermissions.Overrides.Read,
            LocalizableString.Create<LocalizationEndpointsLocalizationResource>(
                "Permission:Localization.Overrides.Read"));

        group.AddPermission(
            LocalizationOverridesPermissions.Overrides.Manage,
            LocalizableString.Create<LocalizationEndpointsLocalizationResource>(
                "Permission:Localization.Overrides.Manage"));
    }
}
