namespace Granit.Presence.Endpoints.Permissions;

/// <summary>
/// Permission constants for the presence module.
/// </summary>
public static class PresencePermissions
{
    /// <summary>Top-level permission group name.</summary>
    public const string GroupName = "Presence";

    /// <summary>Permissions allowing a user to manage their own presence.</summary>
    public static class Self
    {
        /// <summary>Grants the right to set or clear one's own manual presence override.</summary>
        public const string Manage = "Presence.Self.Manage";
    }

    /// <summary>Permissions allowing a user to read other users' presence.</summary>
    public static class Users
    {
        /// <summary>Grants the right to read another user's presence snapshot (single or batch).</summary>
        public const string Read = "Presence.Users.Read";
    }

    /// <summary>Permissions covering resource-scoped presence rooms (multi-user awareness).</summary>
    public static class Rooms
    {
        /// <summary>Grants the right to read a resource room — the list of users currently present.</summary>
        public const string Read = "Presence.Rooms.Read";

        /// <summary>Grants the right to join (heartbeat) or leave a resource room.</summary>
        public const string Join = "Presence.Rooms.Join";
    }
}
