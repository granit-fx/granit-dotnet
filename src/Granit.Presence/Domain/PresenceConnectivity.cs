namespace Granit.Presence.Domain;

/// <summary>
/// Auto-detected connectivity component of presence, derived from the client heartbeat.
/// </summary>
public enum PresenceConnectivity
{
    /// <summary>Active heartbeat recently and reported activity recently.</summary>
    Online,

    /// <summary>Active heartbeat but user reported being idle for longer than the away threshold.</summary>
    Away,

    /// <summary>No heartbeat within the offline threshold.</summary>
    Offline,
}
