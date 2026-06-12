namespace Granit.Identity;

/// <summary>
/// Coarse risk classification for a session, surfaced to users and used to drive security responses
/// (notifications, step-up authentication).
/// </summary>
public enum UserSessionRiskLevel
{
    /// <summary>No anomaly detected (also the value when anomaly detection is not installed).</summary>
    None,

    /// <summary>Minor anomaly; informational.</summary>
    Low,

    /// <summary>Notable anomaly; worth surfacing to the user.</summary>
    Medium,

    /// <summary>Strong anomaly (e.g. impossible travel); warrants a security response.</summary>
    High,
}
