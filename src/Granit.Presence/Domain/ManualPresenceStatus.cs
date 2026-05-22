namespace Granit.Presence.Domain;

/// <summary>
/// Manual override a user can apply to their own presence.
/// <see cref="Available"/> means "no override — let connectivity decide".
/// </summary>
public enum ManualPresenceStatus
{
    /// <summary>No manual override; effective status is derived from connectivity.</summary>
    Available = 0,

    /// <summary>Visible as Busy. Push channels still deliver.</summary>
    Busy = 1,

    /// <summary>Visible as DoNotDisturb. Push channels suppressed (when the gate package is loaded).</summary>
    DoNotDisturb = 2,

    /// <summary>Broadcast as Offline to other users. Push channels suppressed.</summary>
    AppearOffline = 3,
}
