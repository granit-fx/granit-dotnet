namespace Granit.BackgroundJobs.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.BackgroundJobs.Endpoints</c> module.
/// Use these names when granting permissions via <c>IPermissionManagerWriter.SetAsync()</c>
/// or when checking access via <c>IPermissionChecker.IsGrantedAsync()</c>.
/// </summary>
public static class BackgroundJobsPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "BackgroundJobs";

    /// <summary>Permissions for the background jobs resource.</summary>
    public static class Jobs
    {
        /// <summary>Grants read-only access to list and view background jobs.</summary>
        public const string Read = "BackgroundJobs.Jobs.Read";

        /// <summary>
        /// Grants access to the write endpoints (pause, resume, trigger).
        /// Read endpoints require <see cref="Read"/> — grant both to operators
        /// who administer jobs through the dashboard.
        /// </summary>
        public const string Manage = "BackgroundJobs.Jobs.Manage";
    }
}
