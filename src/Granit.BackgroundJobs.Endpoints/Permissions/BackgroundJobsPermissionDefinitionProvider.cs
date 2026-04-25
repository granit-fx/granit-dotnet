using Granit.Authorization;
using Granit.BackgroundJobs.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

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

        // Host-only: the Wolverine scheduler is a single instance per application, jobs
        // are registered globally via [RecurringJob] with kebab-case names unique across
        // the whole app (e.g. "blob-storage-orphan-cleanup", "privacy-export-expiration").
        // Their state and controls are cross-tenant by construction — exposing trigger /
        // pause / cancel to a tenant would leak other tenants' activity and let one tenant
        // interrupt work scheduled for others. Reading the job list has the same leak.
        group.AddPermission(
            BackgroundJobsPermissions.Jobs.Read,
            LocalizableString.Create<BackgroundJobsEndpointsLocalizationResource>(
                "Permission:BackgroundJobs.Jobs.Read"),
            MultiTenancySides.Host);

        group.AddPermission(
            BackgroundJobsPermissions.Jobs.Manage,
            LocalizableString.Create<BackgroundJobsEndpointsLocalizationResource>(
                "Permission:BackgroundJobs.Jobs.Manage"),
            MultiTenancySides.Host);
    }
}
