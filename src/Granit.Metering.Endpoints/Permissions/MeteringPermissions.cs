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

    /// <summary>Raw <c>MeterEvent</c> administrative permissions.</summary>
    public static class Events
    {
        /// <summary>
        /// Manage individual meter events — currently used by the soft-deprecation
        /// endpoint. Deliberately separated from <see cref="Usage.Record"/> (which
        /// gates ingestion) because deprecation is destructive at the billing-data
        /// level and must be restricted to admins (ISO 27001 A.9.4 — least
        /// privilege on event tampering operations).
        /// </summary>
        public const string Manage = "Metering.Events.Manage";

        /// <summary>
        /// Backfill historical events older than the standard 7-day ingestion
        /// window (up to 365 days). Deliberately separated from
        /// <see cref="Usage.Record"/> because backfilling triggers automatic
        /// recomputes on past <c>UsageAggregate</c> rows — destructive at the
        /// billing-data level (ISO 27001 A.9.4 — least privilege).
        /// </summary>
        public const string Backfill = "Metering.Events.Backfill";
    }
}
