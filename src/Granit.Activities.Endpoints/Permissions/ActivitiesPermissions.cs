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
        /// <summary>List + get-by-id (own activities — assignee == caller or 'me' filter).</summary>
        public const string Read = "Activities.Activities.Read";

        /// <summary>List activities assigned to other users — separate gate for peer-workload queries.</summary>
        public const string ReadOthers = "Activities.Activities.ReadOthers";

        /// <summary>Create / cancel / reschedule.</summary>
        public const string Manage = "Activities.Activities.Manage";

        /// <summary>Reassign — separate gate so least-privilege roles cannot move work to other users.</summary>
        public const string Reassign = "Activities.Activities.Reassign";

        /// <summary>Complete (typically held by the assignee in addition to <see cref="Read"/>).</summary>
        public const string Execute = "Activities.Activities.Execute";
    }
}
