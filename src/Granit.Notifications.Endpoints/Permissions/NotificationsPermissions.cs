namespace Granit.Notifications.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Notifications.Endpoints</c> module.
/// Use these names when granting permissions via <c>IPermissionManagerWriter.SetAsync()</c>
/// or when checking access via <c>IPermissionChecker.IsGrantedAsync()</c>.
/// </summary>
public static class NotificationsPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Notifications";

    /// <summary>Permissions for user notifications.</summary>
    public static class UserNotifications
    {
        /// <summary>Grants read-only access to view notifications (inbox, activity feed).</summary>
        public const string Read = "Notifications.UserNotifications.Read";

        /// <summary>Grants management access to notification settings (preferences, subscriptions, push tokens).</summary>
        public const string Manage = "Notifications.UserNotifications.Manage";
    }
}
