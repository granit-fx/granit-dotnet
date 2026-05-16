namespace Granit.Timeline.Abstractions;

/// <summary>
/// Stable RFC 4122 namespace identifiers used to derive deterministic v5
/// GUIDs in the Timeline module.
/// </summary>
public static class TimelineGuidNamespaces
{
    /// <summary>
    /// Namespace for shadow-row Ids — the v5 hash of
    /// <c>(TenantId | EntityType | EntityId | SourceKey | SourceId)</c> under
    /// this namespace yields the canonical shadow <c>TimelineEntry.Id</c>.
    /// Stable forever — never rotate, or every existing shadow becomes
    /// unreachable.
    /// </summary>
    public static readonly Guid Shadow = new("e2c4b3d2-5a8f-5b6e-9c3a-2c8d7f1e4b90");
}
