namespace Granit.Subscriptions.Endpoints.Permissions;

/// <summary>Permission constants for Granit.Subscriptions.Endpoints.</summary>
public static class SubscriptionsPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Subscriptions";

    /// <summary>Plan resource permissions.</summary>
    public static class Plans
    {
        /// <summary>View plans.</summary>
        public const string Read = "Subscriptions.Plans.Read";

        /// <summary>Manage plans (create, publish, archive).</summary>
        public const string Manage = "Subscriptions.Plans.Manage";
    }

    /// <summary>Subscription resource permissions.</summary>
    public static class Subscriptions
    {
        /// <summary>View subscriptions.</summary>
        public const string Read = "Subscriptions.Subscriptions.Read";

        /// <summary>Manage subscriptions (create, cancel, change plan).</summary>
        public const string Manage = "Subscriptions.Subscriptions.Manage";
    }

    /// <summary>Seat resource permissions.</summary>
    public static class Seats
    {
        /// <summary>View seat assignments.</summary>
        public const string Read = "Subscriptions.Seats.Read";

        /// <summary>Manage seat assignments (assign, revoke).</summary>
        public const string Manage = "Subscriptions.Seats.Manage";
    }
}
