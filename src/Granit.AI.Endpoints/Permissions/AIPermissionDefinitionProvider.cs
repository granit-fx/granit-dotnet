using Granit.AI.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;

namespace Granit.AI.Endpoints.Permissions;

/// <summary>
/// Declares the <c>AI.*</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <see cref="GranitAIEndpointsModule"/>.
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
/// <item>Call <c>IPermissionManagerWriter.SetAsync("AI.Workspaces.Read", "my-role", tenantId, true)</c></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class AIPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AIPermissions.GroupName,
            LocalizableString.Create<AIEndpointsLocalizationResource>(
                "PermissionGroup:AI"));

        group.AddPermission(
            AIPermissions.Workspaces.Read,
            LocalizableString.Create<AIEndpointsLocalizationResource>(
                "Permission:AI.Workspaces.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            AIPermissions.Workspaces.Manage,
            LocalizableString.Create<AIEndpointsLocalizationResource>(
                "Permission:AI.Workspaces.Manage"),
            MultiTenancySide.Both);

        group.AddPermission(
            AIPermissions.Usage.Read,
            LocalizableString.Create<AIEndpointsLocalizationResource>(
                "Permission:AI.Usage.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            AIPermissions.Chat.Execute,
            LocalizableString.Create<AIEndpointsLocalizationResource>(
                "Permission:AI.Chat.Execute"),
            MultiTenancySide.Both);

        group.AddPermission(
            AIPermissions.Embeddings.Execute,
            LocalizableString.Create<AIEndpointsLocalizationResource>(
                "Permission:AI.Embeddings.Execute"),
            MultiTenancySide.Both);
    }
}
