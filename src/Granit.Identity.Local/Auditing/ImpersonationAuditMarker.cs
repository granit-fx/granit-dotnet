namespace Granit.Identity.Local.Auditing;

/// <summary>
/// Shared marker identifying administrator impersonation on the durable audit trail.
/// </summary>
/// <remarks>
/// The admin impersonation endpoint stamps <see cref="AuditEntityType"/> as the synthetic entity
/// type of the impersonation audit entry, and the transparency-notification handler uses it to
/// recognise impersonation entries in the <c>AuditEntryPersistedEto</c> stream — so the notification
/// derives from the persisted audit record rather than a separate best-effort event. Kept here (base
/// module, no auditing dependency) so both the writer (<c>.Endpoints</c>) and the reader
/// (<c>.Notifications</c>) share one constant instead of a drift-prone magic string.
/// </remarks>
public static class ImpersonationAuditMarker
{
    /// <summary>Synthetic <c>AuditEntityChange.EntityType</c> recorded for an impersonation.</summary>
    public const string AuditEntityType = "Impersonation";
}
