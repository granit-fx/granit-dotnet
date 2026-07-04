namespace Granit.Presence.Domain;

/// <summary>
/// Effective presence status broadcast to other users.
/// Derived by <see cref="Abstractions.IPresenceQueryService"/> from the user's
/// manual override and live connectivity signal.
/// </summary>
public enum PresenceStatus
{
    /// <summary>User is connected and recently active.</summary>
    Online,

    /// <summary>User is connected but has been idle longer than the configured threshold.</summary>
    Away,

    /// <summary>User has no active connection (or has manually chosen to appear offline).</summary>
    Offline,

    /// <summary>User is online but has marked themselves as busy. Push notifications still delivered.</summary>
    Busy,

    /// <summary>User has muted intrusive push notifications. Store-and-forward channels keep delivering.</summary>
    DoNotDisturb,
}
