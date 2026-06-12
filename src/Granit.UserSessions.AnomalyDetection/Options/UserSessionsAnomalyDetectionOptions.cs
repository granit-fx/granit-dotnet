using System.ComponentModel.DataAnnotations;

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
    [Range(1, int.MaxValue)]
    public int MaxAiCallsPerHourPerTenant { get; set; } = 500;

    /// <summary>
    /// Hard cap on AI calls per user per hour, enforced alongside <see cref="MaxAiCallsPerHourPerTenant"/>. Bounds
    /// the blast radius of a single noisy user: without it, one subject could exhaust the whole tenant budget and
    /// silently downgrade every other user's detection to heuristics. Over the cap, that user degrades to
    /// heuristics while others keep the AI layer. Default: 50.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxAiCallsPerHourPerUser { get; set; } = 50;

    /// <summary>Per-call AI timeout in seconds. On timeout, detection degrades to heuristics.</summary>
    [Range(1, 600)]
    public int AiTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum plausible travel speed (km/h) between two sessions before it is flagged as impossible travel.
    /// Default: 1000 (faster than a commercial flight plus airport time).
    /// </summary>
    [Range(1d, double.MaxValue)]
    public double MaxTravelKilometersPerHour { get; set; } = 1000d;
}
