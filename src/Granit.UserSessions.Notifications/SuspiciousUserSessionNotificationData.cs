namespace Granit.UserSessions.Notifications;

/// <summary>
/// Data payload for a suspicious / new-session alert email. Built from
/// <c>SuspiciousUserSessionDetectedEto</c> at handle time so the template renders the user-facing
/// context (where, why, when, on what device) without re-resolving anything.
/// </summary>
/// <param name="Reason">Primary machine-readable reason code (the most significant of the Eto's
/// reasons, e.g. <c>"impossible_travel"</c>). Drives the plain-language explanation in the template.</param>
/// <param name="ReasonsDisplay">All reason codes joined into a single string. A flat string is
/// required because an <c>IReadOnlyList&lt;string&gt;</c> does not iterate in Scriban after the
/// notification payload is flattened to a dictionary.</param>
/// <param name="City">Approximate city of the sign-in, when resolved; otherwise <see langword="null"/>.</param>
/// <param name="CountryCode">Approximate country (ISO code) of the sign-in, when resolved.</param>
/// <param name="Device">Raw User-Agent of the sign-in, when available. Rendered as-is for now —
/// a friendly device label ("Chrome on Windows") is a follow-up; no user-agent parser exists yet.</param>
/// <param name="DetectedAt">When the verdict was produced (rendered in UTC; user-timezone
/// localization is a follow-up).</param>
public sealed record SuspiciousUserSessionNotificationData(
    string Reason,
    string ReasonsDisplay,
    string? City,
    string? CountryCode,
    string? Device,
    DateTimeOffset DetectedAt);
