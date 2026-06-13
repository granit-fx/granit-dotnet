namespace Granit.Identity.Notifications;

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
/// <param name="IpAddress">Raw client IP of the sign-in, present only when the deployment opted into raw-IP
/// exposure (<c>Identity:AnomalyDetection:IncludeClientIpInAlert</c>); otherwise <see langword="null"/> and
/// the IP row is omitted. Personal data — never log it.</param>
/// <param name="Browser">Coarse browser family ("Chrome", "Safari") derived from the raw User-Agent via
/// <see cref="Internal.UserAgentDescriptor"/>; <see langword="null"/> when it could not be derived. Kept
/// separate from <paramref name="OperatingSystem"/> so the template composes and labels them per culture (no
/// English connector baked in code). Never the raw User-Agent string.</param>
/// <param name="OperatingSystem">Coarse OS family ("Windows", "iPhone") derived from the raw User-Agent;
/// <see langword="null"/> when it could not be derived.</param>
/// <param name="DetectedAt">When the verdict was produced. The template renders it in the recipient's
/// time zone (via the <c>to_user_time</c> Scriban function) with the IANA zone shown in parentheses,
/// falling back to UTC when the recipient has no time zone configured.</param>
/// <param name="ReviewToken">Signed, single-use "was this you?" token, when the session-review token service is
/// registered (the <c>Granit.Identity.Endpoints</c> package). The template composes the review-page link from
/// it (<c>{{ app.base_url }}/.../review?token={{ model.review_token }}</c>); <see langword="null"/> when no
/// token service is available, in which case the template omits the review CTA.</param>
public sealed record SuspiciousUserSessionNotificationData(
    string Reason,
    string ReasonsDisplay,
    string? City,
    string? CountryCode,
    string? IpAddress,
    string? Browser,
    string? OperatingSystem,
    DateTimeOffset DetectedAt,
    string? ReviewToken = null);
