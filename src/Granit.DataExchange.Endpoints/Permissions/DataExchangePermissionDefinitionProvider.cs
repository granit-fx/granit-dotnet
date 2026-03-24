using Granit.Authorization.Abstractions;
using Granit.DataExchange.Endpoints.Internal;
using Granit.Localization;

namespace Granit.DataExchange.Endpoints.Permissions;

/// <summary>
/// Declares the <c>DataExchange.Imports.*</c> and <c>DataExchange.Exports.*</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <see cref="GranitDataExchangeEndpointsModule"/>.
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
/// <item>Call <c>IPermissionManagerWriter.SetAsync("DataExchange.Imports.Execute", "my-role", tenantId, true)</c></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DataExchangePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            DataExchangePermissions.GroupName,
            LocalizableString.Create<DataExchangeEndpointsLocalizationResource>(
                "PermissionGroup:DataExchange"));

        group.AddPermission(
            DataExchangePermissions.Imports.Read,
            LocalizableString.Create<DataExchangeEndpointsLocalizationResource>(
                "Permission:DataExchange.Imports.Read"));

        group.AddPermission(
            DataExchangePermissions.Imports.Execute,
            LocalizableString.Create<DataExchangeEndpointsLocalizationResource>(
                "Permission:DataExchange.Imports.Execute"));

        group.AddPermission(
            DataExchangePermissions.Exports.Read,
            LocalizableString.Create<DataExchangeEndpointsLocalizationResource>(
                "Permission:DataExchange.Exports.Read"));

        group.AddPermission(
            DataExchangePermissions.Exports.Execute,
            LocalizableString.Create<DataExchangeEndpointsLocalizationResource>(
                "Permission:DataExchange.Exports.Execute"));
    }
}
