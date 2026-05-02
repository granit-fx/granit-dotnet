namespace Granit.Timeline.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Timeline.Endpoints</c> module.
/// Use these names when granting permissions via <c>IPermissionManagerWriter.SetAsync()</c>
/// or when checking access via <c>IPermissionChecker.IsGrantedAsync()</c>.
/// </summary>
public static class TimelinePermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Timeline";

    /// <summary>Permissions for the timeline entries resource.</summary>
    public static class Entries
    {
        /// <summary>Grants read access to activity streams, followers, and timeline history.</summary>
        public const string Read = "Timeline.Entries.Read";

        /// <summary>Grants access to create timeline entries (post comments).</summary>
        public const string Create = "Timeline.Entries.Create";

        /// <summary>Grants access to soft-delete any timeline entry regardless of ownership (admin).</summary>
        public const string Manage = "Timeline.Entries.Manage";
    }

    /// <summary>Permissions for the internal notes resource (staff-only entries).</summary>
    public static class InternalNotes
    {
        /// <summary>Grants read access to internal notes in activity streams.</summary>
        public const string Read = "Timeline.InternalNotes.Read";
    }

    /// <summary>Permissions for the followers resource.</summary>
    public static class Followers
    {
        /// <summary>Grants access to follow/unfollow entities and view follower lists.</summary>
        public const string Manage = "Timeline.Followers.Manage";
    }

    /// <summary>Permissions for the reactions resource (story C2 / ADR-046 §1).</summary>
    public static class Reactions
    {
        /// <summary>
        /// Grants access to read the reactions catalog and toggle reactions on
        /// timeline entries via <c>POST /timeline/entries/{id}/reactions/{emoji}</c>.
        /// Catalog read requires the same permission (defense in depth — a
        /// user who cannot react does not need to see the picker).
        /// </summary>
        public const string React = "Timeline.Reactions.React";
    }
}
