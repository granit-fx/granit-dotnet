namespace Granit.Metering.Endpoints.Permissions;

/// <summary>Permission constants for Granit.Metering.Endpoints.</summary>
public static class MeteringPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Metering";

    /// <summary>Meter definition resource permissions.</summary>
    public static class Meters
    {
        /// <summary>View meter definitions.</summary>
        public const string Read = "Metering.Meters.Read";

        /// <summary>Manage meter definitions (create, update, deactivate).</summary>
        public const string Manage = "Metering.Meters.Manage";
    }

    /// <summary>Usage data resource permissions.</summary>
    public static class Usage
    {
        /// <summary>View usage data and aggregates.</summary>
        public const string Read = "Metering.Usage.Read";

        /// <summary>Record usage events.</summary>
        public const string Record = "Metering.Usage.Record";
    }
}
