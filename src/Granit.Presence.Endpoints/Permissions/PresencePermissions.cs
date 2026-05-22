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
}
