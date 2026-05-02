namespace Granit.Activities.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Activities.Endpoints</c> module.
/// </summary>
public static class ActivitiesPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Activities";

    /// <summary>Permissions for the activities resource.</summary>
    public static class Activities
    {
        /// <summary>List + get-by-id.</summary>
        public const string Read = "Activities.Activities.Read";

        /// <summary>Create / cancel / reassign / reschedule.</summary>
        public const string Manage = "Activities.Activities.Manage";

        /// <summary>Complete (typically held by the assignee in addition to <see cref="Read"/>).</summary>
        public const string Execute = "Activities.Activities.Execute";
    }
}
