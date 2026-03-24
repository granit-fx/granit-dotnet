using Granit.Authorization.Abstractions;
using Granit.BackgroundJobs.Endpoints.Internal;
using Granit.Localization;

namespace Granit.BackgroundJobs.Endpoints.Permissions;

/// <summary>
/// Declares the <c>BackgroundJobs.Jobs.Read</c> and <c>BackgroundJobs.Jobs.Manage</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <see cref="GranitBackgroundJobsEndpointsModule"/>.
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
/// <item>Call <c>IPermissionManagerWriter.SetAsync("BackgroundJobs.Jobs.Manage", "my-role", tenantId, true)</c></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class BackgroundJobsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            BackgroundJobsPermissions.GroupName,
            LocalizableString.Create<BackgroundJobsEndpointsLocalizationResource>(
                "PermissionGroup:BackgroundJobs"));

        group.AddPermission(
            BackgroundJobsPermissions.Jobs.Read,
            LocalizableString.Create<BackgroundJobsEndpointsLocalizationResource>(
                "Permission:BackgroundJobs.Jobs.Read"));

        group.AddPermission(
            BackgroundJobsPermissions.Jobs.Manage,
            LocalizableString.Create<BackgroundJobsEndpointsLocalizationResource>(
                "Permission:BackgroundJobs.Jobs.Manage"));
    }
}
