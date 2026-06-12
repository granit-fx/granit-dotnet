namespace Granit.UserSessions.AnomalyDetection.Options;

/// <summary>
/// Configuration for session anomaly detection, bound from <c>"UserSessions:AnomalyDetection"</c>.
/// </summary>
public sealed class UserSessionsAnomalyDetectionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "UserSessions:AnomalyDetection";

    /// <summary>
    /// When <c>true</c>, augments the always-on heuristics with an AI assessment via
    /// <c>Granit.AI</c> <c>IStructuredCompletion</c>. <strong>Opt-in</strong> because it may send coarse session
    /// features to a third-party model provider (never the raw IP). Default: <c>false</c>.
    /// </summary>
    public bool UseAi { get; set; }

    /// <summary>AI workspace name. <c>null</c> uses the configured default workspace.</summary>
    public string? WorkspaceName { get; set; }

    /// <summary>Hard cap on AI calls per tenant per hour. Over the cap, detection degrades to heuristics.</summary>
    public int MaxAiCallsPerHourPerTenant { get; set; } = 500;

    /// <summary>Per-call AI timeout in seconds. On timeout, detection degrades to heuristics.</summary>
    public int AiTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum plausible travel speed (km/h) between two sessions before it is flagged as impossible travel.
    /// Default: 1000 (faster than a commercial flight plus airport time).
    /// </summary>
    public double MaxTravelKilometersPerHour { get; set; } = 1000d;
}
