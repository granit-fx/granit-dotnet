namespace Granit.Scheduling.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Scheduling.Endpoints</c> module.
/// </summary>
public static class SchedulingPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Scheduling";

    /// <summary>Permissions for the scheduled actions resource.</summary>
    public static class Actions
    {
        /// <summary>Grants read-only access to list and view scheduled actions.</summary>
        public const string Read = "Scheduling.Actions.Read";

        /// <summary>Grants management access (cancel, reschedule) to scheduled actions.</summary>
        public const string Manage = "Scheduling.Actions.Manage";
    }
}
